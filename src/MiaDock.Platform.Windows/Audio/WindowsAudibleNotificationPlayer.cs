using System.Runtime.InteropServices;
using MiaDock.Core.Audio;
using MiaDock.Core.Logging;
using MiaDock.Core.Modules;
using MiaDock.Core.Settings;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MiaDock.Platform.Windows.Audio;

public sealed class WindowsAudibleNotificationPlayer : IAudibleNotificationPlayer
{
    internal static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(2);

    private static readonly IReadOnlyDictionary<AudibleNotificationCue, string> FileNames =
        new Dictionary<AudibleNotificationCue, string>
        {
            [AudibleNotificationCue.NetworkOffline] = "connected-internet-none.wav",
            [AudibleNotificationCue.ConnectedWithoutInternet] = "connected-but-no-internet.wav",
            [AudibleNotificationCue.LowBattery] = "low-battery.wav",
            [AudibleNotificationCue.DeviceConnected] = "device-connected.wav",
            [AudibleNotificationCue.DeviceDisconnected] = "device-left.wav",
            [AudibleNotificationCue.Hourly] = "per-hour-per.wav"
        };

    private readonly object _gate = new();
    private readonly string _soundDirectory;
    private readonly Func<IAudibleNotificationPlaybackSession> _sessionFactory;
    private readonly Func<string, bool> _fileExists;
    private readonly TimeProvider _timeProvider;
    private readonly ILogService? _log;
    private IAudibleNotificationPlaybackSession? _session;
    private AudibleNotificationCue _lastCue;
    private DateTimeOffset _lastCueAt;
    private bool _disposed;

    public WindowsAudibleNotificationPlayer(IAudibleNotificationSettingsProvider settings, ILogService? log = null)
        : this(
            Path.Combine(AppContext.BaseDirectory, "Assets", "sfx"),
            () => new NaudioAudibleNotificationSession(() => settings.Current, log),
            File.Exists,
            TimeProvider.System,
            log)
    {
    }

    internal WindowsAudibleNotificationPlayer(
        string soundDirectory,
        Func<IAudibleNotificationPlaybackSession> sessionFactory,
        Func<string, bool> fileExists,
        TimeProvider timeProvider,
        ILogService? log = null)
    {
        _soundDirectory = soundDirectory;
        _sessionFactory = sessionFactory;
        _fileExists = fileExists;
        _timeProvider = timeProvider;
        _log = log;
    }

    public void Play(AudibleNotificationCue cue) => PlayCore(cue, isPreview: false);

    public void Preview(AudibleNotificationCue cue) => PlayCore(cue, isPreview: true);

    public void Stop()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _session?.Stop();
            }
            catch (Exception exception)
            {
                LogFailure("stop", _lastCue, exception);
            }
        }
    }

    public void Dispose()
    {
        IAudibleNotificationPlaybackSession? session;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            session = _session;
            _session = null;
        }

        if (session is null)
        {
            return;
        }

        session.Failed -= OnPlaybackFailed;
        try
        {
            session.Dispose();
        }
        catch (Exception exception)
        {
            LogFailure("dispose", _lastCue, exception);
        }
    }

    private void PlayCore(AudibleNotificationCue cue, bool isPreview)
    {
        if (cue == AudibleNotificationCue.None || !FileNames.TryGetValue(cue, out var fileName))
        {
            return;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var now = _timeProvider.GetUtcNow();
            if (!isPreview && cue == _lastCue && now - _lastCueAt < DuplicateWindow)
            {
                return;
            }

            if (!isPreview)
            {
                _lastCue = cue;
                _lastCueAt = now;
            }

            try
            {
                _session?.Stop();
            }
            catch (Exception exception)
            {
                LogFailure("replace", cue, exception);
            }

            var path = Path.Combine(_soundDirectory, fileName);
            if (!_fileExists(path))
            {
                LogFailure("file-missing", cue, null);
                return;
            }

            try
            {
                _session ??= CreateSession();
                _session.Play(new Uri(Path.GetFullPath(path), UriKind.Absolute));
            }
            catch (Exception exception)
            {
                LogFailure("play", cue, exception);
            }
        }
    }

    private IAudibleNotificationPlaybackSession CreateSession()
    {
        var session = _sessionFactory();
        session.Failed += OnPlaybackFailed;
        return session;
    }

    private void OnPlaybackFailed(object? sender, EventArgs args) =>
        LogFailure("media-failed", _lastCue, null);

    private void LogFailure(string operation, AudibleNotificationCue cue, Exception? exception) =>
        _log?.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.AudibleNotificationPlaybackFailed,
            "NotificationSound",
            "A notification sound could not be played and was skipped safely.",
            properties: new Dictionary<string, object?>
            {
                ["operation"] = operation,
                ["cue"] = cue.ToString(),
                ["errorType"] = exception?.GetType().Name
            });
}

internal interface IAudibleNotificationPlaybackSession : IDisposable
{
    event EventHandler? Failed;

    void Play(Uri source);

    void Stop();
}

internal sealed class NaudioAudibleNotificationSession : IAudibleNotificationPlaybackSession
{
    private readonly Func<AudibleNotificationSettings> _settings;
    private readonly ILogService? _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WasapiPlayer? _player;
    private AudioFileReader? _reader;
    private bool _disposed;

    internal NaudioAudibleNotificationSession(
        Func<AudibleNotificationSettings> settings,
        ILogService? log)
    {
        _settings = settings;
        _log = log;
    }

    public event EventHandler? Failed;

    public void Play(Uri source) => _ = PlayAsync(source);

    public void Stop() => _ = StopAsync();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
        _gate.Dispose();
    }

    private async Task PlayAsync(Uri source)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            await StopCoreAsync().ConfigureAwait(false);
            var options = _settings();
            var builder = new WasapiPlayerBuilder().WithSharedMode().WithEventSync();
            var usedFallback = false;
            if (!string.IsNullOrWhiteSpace(options.OutputDeviceId))
            {
                try
                {
                    using var devices = new MMDeviceEnumerator();
                    builder.WithDevice(devices.GetDevice(options.OutputDeviceId));
                }
                catch (Exception)
                {
                    usedFallback = true;
                }
            }

            var player = builder.Build();
            var reader = new AudioFileReader(source.LocalPath);
            player.Init(reader);
            player.Volume = options.VolumePercent / 100f;
            player.PlaybackStopped += OnPlaybackStopped;
            _player = player;
            _reader = reader;
            player.Play();
            if (usedFallback)
            {
                _log?.Write(TechnicalLogLevel.Warning, TechnicalEventIds.AudibleNotificationPlaybackFailed,
                    "NotificationSound", "The selected notification device was unavailable; the default output was used.",
                    properties: new Dictionary<string, object?> { ["operation"] = "fallback-default-device" });
            }
        }
        catch (Exception)
        {
            Failed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopAsync()
    {
        if (_disposed && _player is null) return;
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await StopCoreAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task StopCoreAsync()
    {
        var player = _player;
        var reader = _reader;
        _player = null;
        _reader = null;
        if (player is not null)
        {
            player.PlaybackStopped -= OnPlaybackStopped;
            await player.DisposeAsync().ConfigureAwait(false);
        }
        reader?.Dispose();
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs args)
    {
        if (args.Exception is not null) Failed?.Invoke(this, EventArgs.Empty);
        _ = ReleaseCompletedAsync(sender as WasapiPlayer);
    }

    private async Task ReleaseCompletedAsync(WasapiPlayer? completed)
    {
        if (completed is null) return;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(_player, completed)) return;
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}
