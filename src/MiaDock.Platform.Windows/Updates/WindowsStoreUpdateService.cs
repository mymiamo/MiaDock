using MiaDock.Core.Logging;
using MiaDock.Core.Updates;

namespace MiaDock.Platform.Windows.Updates;

public sealed class WindowsStoreUpdateService : IStoreUpdateService
{
    private readonly IStoreUpdateClient _client;
    private readonly ILogService? _log;
    private readonly SemaphoreSlim _checkGate = new(1, 1);

    public WindowsStoreUpdateService(ILogService log)
        : this(new WindowsStoreUpdateClient(), log)
    {
    }

    internal WindowsStoreUpdateService(
        IStoreUpdateClient client,
        ILogService? log = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _log = log;
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

            if (!_client.HasInternetAccess)
            {
                return Publish(new StoreUpdateSnapshot(
                    StoreUpdateStatus.Offline,
                    _client.CurrentVersion,
                    CheckedAtUtc: DateTimeOffset.UtcNow));
            }

            Publish(new StoreUpdateSnapshot(
                StoreUpdateStatus.Checking,
                _client.CurrentVersion));

            try
            {
                var versions = await _client
                    .GetAvailableVersionsAsync(cancellationToken)
                    .ConfigureAwait(false);
                var currentVersion = StoreUpdateSnapshot.Normalize(_client.CurrentVersion);
                var availableVersion = versions
                    .Select(StoreUpdateSnapshot.Normalize)
                    .Where(version => version.CompareTo(currentVersion) > 0)
                    .OrderDescending()
                    .FirstOrDefault();
                var result = new StoreUpdateSnapshot(
                    availableVersion is null
                        ? StoreUpdateStatus.UpToDate
                        : StoreUpdateStatus.UpdateAvailable,
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
                        ["updateCount"] = versions.Count
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
                return Publish(new StoreUpdateSnapshot(
                    StoreUpdateStatus.Failed,
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
            return await _client
                .OpenStorePageAsync(cancellationToken)
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
}
