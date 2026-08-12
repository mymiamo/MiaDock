using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using MiaDock.Core.Logging;
using MiaDock.Core.Threading;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;
using Microsoft.Win32;

namespace MiaDock.Platform.Windows.Privacy;

public sealed class WindowsPrivacyUsageService : IPrivacyUsageService
{
    private const string ConsentStoreRoot =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly HashSet<string> IgnoredProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System",
        "Registry",
        "Idle",
        "svchost",
        "RuntimeBroker",
        "ShellExperienceHost",
        "SearchHost",
        "StartMenuExperienceHost",
        "TextInputHost"
    };

    private readonly IUiDispatcher _dispatcher;
    private readonly ILogService? _log;
    private readonly object _gate = new();
    private readonly Dictionary<string, CachedIdentity> _identityCache = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _lifetime;
    private Task? _monitorTask;
    private PrivacyState _current = PrivacyState.Empty;
    private bool _started;
    private bool _disposed;

    public WindowsPrivacyUsageService(IUiDispatcher dispatcher, ILogService? log = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _log = log;
    }

    public PrivacyState Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public event EventHandler<PrivacyState>? StateChanged;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            if (_started)
            {
                return Task.CompletedTask;
            }

            _started = true;
            _lifetime = new CancellationTokenSource();
            var token = _lifetime.Token;
            _monitorTask = Task.Run(() => MonitorLoopAsync(token), CancellationToken.None);
        }

        Publish(Scan());
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancellationTokenSource? lifetime;
        Task? monitorTask;
        lock (_gate)
        {
            lifetime = _lifetime;
            monitorTask = _monitorTask;
            _lifetime = null;
            _monitorTask = null;
            _started = false;
        }

        try
        {
            lifetime?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        if (monitorTask is not null)
        {
            try
            {
                await monitorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _log?.Write(
                    TechnicalLogLevel.Warning,
                    TechnicalEventIds.PrivacyUsageMonitorFailed,
                    "Privacy",
                    "Privacy usage monitor stopped with an error.",
                    exception);
            }
        }

        lifetime?.Dispose();
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        using var micEvent = new ManualResetEvent(false);
        using var camEvent = new ManualResetEvent(false);
        using var micKey = TryOpenConsentKey("microphone");
        using var camKey = TryOpenConsentKey("webcam");
        RegisterWatch(micKey, micEvent);
        RegisterWatch(camKey, camEvent);

        var handles = new WaitHandle[] { micEvent, camEvent, cancellationToken.WaitHandle };
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                WaitHandle.WaitAny(handles, PollInterval);
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Publish(Scan());
                RegisterWatch(micKey, micEvent);
                RegisterWatch(camKey, camEvent);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _log?.Write(
                    TechnicalLogLevel.Warning,
                    TechnicalEventIds.PrivacyUsageMonitorFailed,
                    "Privacy",
                    "Privacy usage scan failed.",
                    exception);
                try
                {
                    await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private PrivacyState Scan()
    {
        var apps = new Dictionary<string, PrivacyApplication>(StringComparer.OrdinalIgnoreCase);
        CollectActive(apps, "microphone", usesMicrophone: true, usesCamera: false);
        CollectActive(apps, "webcam", usesMicrophone: false, usesCamera: true);
        EnrichProcessIds(apps);
        return PrivacyState.FromApplications(apps.Values);
    }

    private void CollectActive(
        IDictionary<string, PrivacyApplication> apps,
        string capability,
        bool usesMicrophone,
        bool usesCamera)
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey($@"{ConsentStoreRoot}\{capability}", writable: false);
            if (root is null)
            {
                return;
            }

            CollectFromKey(apps, root, usesMicrophone, usesCamera, packaged: true);
            using var nonPackaged = root.OpenSubKey("NonPackaged", writable: false);
            if (nonPackaged is not null)
            {
                CollectFromKey(apps, nonPackaged, usesMicrophone, usesCamera, packaged: false);
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException)
        {
            _log?.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.PrivacyUsageMonitorFailed,
                "Privacy",
                $"ConsentStore read failed for {capability}.",
                exception);
        }
    }

    private void CollectFromKey(
        IDictionary<string, PrivacyApplication> apps,
        RegistryKey key,
        bool usesMicrophone,
        bool usesCamera,
        bool packaged)
    {
        foreach (var name in key.GetSubKeyNames())
        {
            if (string.Equals(name, "NonPackaged", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var child = key.OpenSubKey(name, writable: false);
            if (child is null)
            {
                continue;
            }

            if (!IsCurrentlyInUse(child))
            {
                // Nested NonPackaged path keys may contain deeper children.
                if (!packaged)
                {
                    CollectFromKey(apps, child, usesMicrophone, usesCamera, packaged: false);
                }

                continue;
            }

            var identity = ResolveIdentity(name, packaged);
            if (identity is null || ShouldIgnore(identity.ProcessName, identity.DisplayName))
            {
                continue;
            }

            if (apps.TryGetValue(identity.Id, out var existing))
            {
                apps[identity.Id] = existing with
                {
                    UsesMicrophone = existing.UsesMicrophone || usesMicrophone,
                    UsesCamera = existing.UsesCamera || usesCamera,
                    ExecutablePath = existing.ExecutablePath ?? identity.ExecutablePath,
                    DisplayName = PreferDisplayName(existing.DisplayName, identity.DisplayName),
                    ProcessName = string.IsNullOrWhiteSpace(existing.ProcessName)
                        ? identity.ProcessName
                        : existing.ProcessName
                };
            }
            else
            {
                apps[identity.Id] = new PrivacyApplication(
                    identity.Id,
                    null,
                    identity.ProcessName,
                    identity.DisplayName,
                    identity.ExecutablePath,
                    usesMicrophone,
                    usesCamera);
            }
        }
    }

    private static bool IsCurrentlyInUse(RegistryKey key)
    {
        var stop = ReadFileTime(key, "LastUsedTimeStop");
        if (stop is null)
        {
            return false;
        }

        // Windows writes 0 while the capability is actively held.
        return stop.Value == 0;
    }

    private static long? ReadFileTime(RegistryKey key, string valueName)
    {
        try
        {
            var value = key.GetValue(valueName);
            return value switch
            {
                long number => number,
                int number => number,
                byte[] bytes when bytes.Length >= 8 => BitConverter.ToInt64(bytes, 0),
                _ => null
            };
        }
        catch (IOException)
        {
            return null;
        }
    }

    private CachedIdentity? ResolveIdentity(string rawName, bool packaged)
    {
        var cacheKey = (packaged ? "pkg:" : "exe:") + rawName;
        lock (_gate)
        {
            if (_identityCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }
        }

        CachedIdentity? resolved = packaged
            ? ResolvePackagedIdentity(rawName)
            : ResolveExecutableIdentity(rawName);

        if (resolved is null)
        {
            return null;
        }

        lock (_gate)
        {
            _identityCache[cacheKey] = resolved;
        }

        return resolved;
    }

    private static CachedIdentity? ResolvePackagedIdentity(string packageFamilyOrAumid)
    {
        if (string.IsNullOrWhiteSpace(packageFamilyOrAumid))
        {
            return null;
        }

        var displayName = packageFamilyOrAumid;
        try
        {
            var info = global::Windows.ApplicationModel.AppInfo.GetFromAppUserModelId(packageFamilyOrAumid);
            if (!string.IsNullOrWhiteSpace(info.DisplayInfo.DisplayName))
            {
                displayName = info.DisplayInfo.DisplayName;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or COMException or UnauthorizedAccessException)
        {
            var underscore = packageFamilyOrAumid.IndexOf('_', StringComparison.Ordinal);
            if (underscore > 0)
            {
                displayName = packageFamilyOrAumid[..underscore];
            }
        }

        return new CachedIdentity(
            "pkg:" + packageFamilyOrAumid,
            displayName,
            displayName,
            null);
    }

    private static CachedIdentity? ResolveExecutableIdentity(string encodedPath)
    {
        var path = DecodeNonPackagedPath(encodedPath);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fileName = Path.GetFileNameWithoutExtension(path) ?? path;
        var displayName = fileName;
        try
        {
            if (File.Exists(path))
            {
                var version = FileVersionInfo.GetVersionInfo(path);
                displayName = FirstNonEmpty(
                    version.FileDescription,
                    version.ProductName,
                    fileName) ?? fileName;
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
        }

        return new CachedIdentity(
            "exe:" + path,
            fileName,
            StripExeSuffix(displayName),
            path);
    }

    private static string DecodeNonPackagedPath(string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return string.Empty;
        }

        // ConsentStore encodes path separators as '#'.
        return encoded.Replace('#', Path.DirectorySeparatorChar);
    }

    private static void EnrichProcessIds(IDictionary<string, PrivacyApplication> apps)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception)
        {
            return;
        }

        try
        {
            var byPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var process in processes)
            {
                try
                {
                    if (process.Id == Environment.ProcessId)
                    {
                        continue;
                    }

                    byName[process.ProcessName] = process.Id;
                    string? path = null;
                    try
                    {
                        path = process.MainModule?.FileName;
                    }
                    catch (Win32Exception)
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        byPath[path] = process.Id;
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }

            foreach (var pair in apps.ToArray())
            {
                var app = pair.Value;
                if (app.ProcessId is > 0)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(app.ExecutablePath) &&
                    byPath.TryGetValue(app.ExecutablePath, out var pathId))
                {
                    apps[pair.Key] = app with { ProcessId = pathId };
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(app.ProcessName) &&
                    byName.TryGetValue(app.ProcessName, out var nameId))
                {
                    apps[pair.Key] = app with { ProcessId = nameId };
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private void Publish(PrivacyState state)
    {
        PrivacyState previous;
        lock (_gate)
        {
            previous = _current;
            if (StatesEqual(previous, state))
            {
                return;
            }

            _current = state;
        }

        _ = _dispatcher.TryEnqueue(() => StateChanged?.Invoke(this, state));
    }

    private static bool StatesEqual(PrivacyState left, PrivacyState right)
    {
        if (left.MicrophoneInUse != right.MicrophoneInUse ||
            left.CameraInUse != right.CameraInUse ||
            left.Indicator != right.Indicator ||
            left.ActiveApplications.Count != right.ActiveApplications.Count)
        {
            return false;
        }

        for (var index = 0; index < left.ActiveApplications.Count; index++)
        {
            var a = left.ActiveApplications[index];
            var b = right.ActiveApplications[index];
            if (!string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase) ||
                a.UsesMicrophone != b.UsesMicrophone ||
                a.UsesCamera != b.UsesCamera ||
                !string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldIgnore(string processName, string displayName)
    {
        if (string.Equals(processName, Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase) ||
            displayName.Contains("MiaDock", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IgnoredProcessNames.Contains(processName);
    }

    private static string PreferDisplayName(string current, string incoming)
    {
        if (LooksLikeRawProcess(current) && !LooksLikeRawProcess(incoming))
        {
            return incoming;
        }

        return string.IsNullOrWhiteSpace(current) ? incoming : current;
    }

    private static bool LooksLikeRawProcess(string value) =>
        value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
        value.Contains('.', StringComparison.Ordinal);

    private static string StripExeSuffix(string value) =>
        value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? value[..^4]
            : value;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static RegistryKey? TryOpenConsentKey(string capability)
    {
        try
        {
            return Registry.CurrentUser.OpenSubKey($@"{ConsentStoreRoot}\{capability}", writable: false);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException)
        {
            return null;
        }
    }

    private static ManualResetEvent CreateManualResetEvent() => new(false);

    private static void RegisterWatch(RegistryKey? key, ManualResetEvent signal)
    {
        if (key is null)
        {
            return;
        }

        try
        {
            signal.Reset();
            var handle = key.Handle.DangerousGetHandle();
            if (handle == IntPtr.Zero)
            {
                return;
            }

            // REG_NOTIFY_CHANGE_NAME | REG_NOTIFY_CHANGE_LAST_SET
            const int filter = 0x00000001 | 0x00000004;
            _ = NativeMethods.RegNotifyChangeKeyValue(
                handle,
                true,
                filter,
                signal.SafeWaitHandle.DangerousGetHandle(),
                true);
        }
        catch (Exception)
        {
            // Polling remains the fallback.
        }
    }

    private sealed record CachedIdentity(
        string Id,
        string ProcessName,
        string DisplayName,
        string? ExecutablePath);

    private static class NativeMethods
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern int RegNotifyChangeKeyValue(
            IntPtr hKey,
            [MarshalAs(UnmanagedType.Bool)] bool bWatchSubtree,
            int dwNotifyFilter,
            IntPtr hEvent,
            [MarshalAs(UnmanagedType.Bool)] bool fAsynchronous);
    }
}
