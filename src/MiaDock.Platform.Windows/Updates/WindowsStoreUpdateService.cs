using MiaDock.Core.Logging;
using MiaDock.Core.Threading;
using MiaDock.Core.Updates;

namespace MiaDock.Platform.Windows.Updates;

public sealed class WindowsStoreUpdateService : IStoreUpdateService
{
    private readonly IStoreUpdateClient _client;
    private readonly ILogService? _log;
    private readonly IUiDispatcher? _dispatcher;
    private readonly SemaphoreSlim _checkGate = new(1, 1);

    public WindowsStoreUpdateService(ILogService log, IUiDispatcher dispatcher)
        : this(new WindowsStoreUpdateClient(), log, dispatcher)
    {
    }

    internal WindowsStoreUpdateService(
        IStoreUpdateClient client,
        ILogService? log = null,
        IUiDispatcher? dispatcher = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _log = log;
        _dispatcher = dispatcher;
        Current = StoreUpdateSnapshot.Unavailable(client.CurrentVersion);
    }

    public StoreUpdateSnapshot Current { get; private set; }

    public event EventHandler<StoreUpdateSnapshot>? UpdateAvailabilityChanged;

    public async Task<StoreUpdateSnapshot> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        await _checkGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_client.HasPackageIdentity)
            {
                return Publish(StoreUpdateSnapshot.Unavailable(_client.CurrentVersion));
            }

            Publish(new StoreUpdateSnapshot(
                StoreUpdateStatus.Checking,
                _client.CurrentVersion));

            try
            {
                var versions = await RunOnUiThreadAsync(
                    () => _client.GetAvailableVersionsAsync(cancellationToken),
                    cancellationToken)
                    .ConfigureAwait(false);
                var currentVersion = StoreUpdateSnapshot.Normalize(_client.CurrentVersion);
                var availableVersion = versions
                    .Select(StoreUpdateSnapshot.Normalize)
                    .Where(version => version.CompareTo(currentVersion) > 0)
                    .OrderDescending()
                    .FirstOrDefault();
                // StoreContext has already filtered this collection to packages
                // with updates available. Trust that signal even if a Store cache
                // returns the installed package version in its metadata.
                var storeReportedUpdate = versions.Count > 0;
                availableVersion ??= storeReportedUpdate ? currentVersion : null;
                var result = new StoreUpdateSnapshot(
                    storeReportedUpdate
                        ? StoreUpdateStatus.UpdateAvailable
                        : StoreUpdateStatus.UpToDate,
                    currentVersion,
                    availableVersion,
                    DateTimeOffset.UtcNow);
                _log?.Write(
                    TechnicalLogLevel.Information,
                    TechnicalEventIds.StoreUpdateCheckCompleted,
                    "StoreUpdate",
                    "Microsoft Store update check completed.",
                    properties: new Dictionary<string, object?>
                    {
                        ["status"] = result.Status.ToString(),
                        ["count"] = versions.Count,
                        ["currentVersion"] = currentVersion.ToString(4),
                        ["availableVersions"] = string.Join(",", versions.Select(version => StoreUpdateSnapshot.Normalize(version).ToString(4))),
                        ["selectedVersion"] = result.AvailableVersion?.ToString(4),
                        ["storeReportedUpdate"] = storeReportedUpdate
                    });
                return Publish(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _log?.Write(
                    TechnicalLogLevel.Warning,
                    TechnicalEventIds.StoreUpdateCheckFailed,
                    "StoreUpdate",
                    "Microsoft Store update check failed.",
                    properties: new Dictionary<string, object?>
                    {
                        ["hresult"] = $"0x{exception.HResult:X8}"
                    });
                var status = !_client.HasInternetAccess || IsNetworkFailure(exception)
                    ? StoreUpdateStatus.Offline
                    : StoreUpdateStatus.Failed;
                return Publish(new StoreUpdateSnapshot(
                    status,
                    _client.CurrentVersion,
                    CheckedAtUtc: DateTimeOffset.UtcNow));
            }
        }
        finally
        {
            _checkGate.Release();
        }
    }

    public async Task<bool> OpenStorePageAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunOnUiThreadAsync(
                    () => _client.OpenStorePageAsync(cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _log?.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.StoreUpdateCheckFailed,
                "StoreUpdate",
                "Microsoft Store product page could not be opened.",
                properties: new Dictionary<string, object?>
                {
                    ["hresult"] = $"0x{exception.HResult:X8}"
                });
            return false;
        }
    }

    private StoreUpdateSnapshot Publish(StoreUpdateSnapshot value)
    {
        Current = value;
        UpdateAvailabilityChanged?.Invoke(this, value);
        return value;
    }

    private Task<T> RunOnUiThreadAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        if (_dispatcher is null || _dispatcher.HasThreadAccess)
        {
            return operation();
        }

        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    completion.TrySetResult(await operation());
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }))
        {
            completion.TrySetException(
                new InvalidOperationException("The UI dispatcher is unavailable."));
        }

        return completion.Task;
    }

    private static bool IsNetworkFailure(Exception exception) =>
        exception.HResult is unchecked((int)0x80072EE2) or
            unchecked((int)0x80072EE7) or
            unchecked((int)0x80072EFD) or
            unchecked((int)0x800704CF) or
            unchecked((int)0x800C0005);
}
