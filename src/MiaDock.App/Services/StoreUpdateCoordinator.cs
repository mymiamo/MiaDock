using MiaDock.App.Modules;
using MiaDock.Core.Logging;
using MiaDock.Core.Threading;
using MiaDock.Core.Updates;

namespace MiaDock.App.Services;

public sealed class StoreUpdateCoordinator : IStoreUpdateCoordinator
{
    internal static readonly TimeSpan InitialCheckDelay = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan AutomaticCheckInterval = TimeSpan.FromHours(4);
    internal static readonly TimeSpan MinimumQueryInterval = TimeSpan.FromMinutes(30);

    private readonly IStoreUpdateService _storeUpdates;
    private readonly ISettingsService _settings;
    private readonly StoreUpdateModule _module;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogService _log;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _checkGate = new(1, 1);
    private Task _loop = Task.CompletedTask;
    private bool _started;
    private bool _disposed;

    public StoreUpdateCoordinator(
        IStoreUpdateService storeUpdates,
        ISettingsService settings,
        StoreUpdateModule module,
        IUiDispatcher dispatcher,
        ILogService log)
    {
        _storeUpdates = storeUpdates;
        _settings = settings;
        _module = module;
        _dispatcher = dispatcher;
        _log = log;
        _storeUpdates.UpdateAvailabilityChanged += OnAvailabilityChanged;
    }

    public StoreUpdateSnapshot Current => _storeUpdates.Current;

    public bool AutomaticChecksEnabled =>
        _settings.Current.StoreUpdates.AutomaticChecksEnabled;

    public event EventHandler<StoreUpdateSnapshot>? UpdateAvailabilityChanged;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        _started = true;
        _loop = RunAutomaticChecksSafelyAsync(_lifetime.Token);
    }

    public Task<StoreUpdateSnapshot> CheckNowAsync(
        CancellationToken cancellationToken = default) =>
        CheckCoreAsync(ignoreAutomaticPreference: true, cancellationToken);

    public Task<bool> OpenStorePageAsync(
        CancellationToken cancellationToken = default) =>
        _storeUpdates.OpenStorePageAsync(cancellationToken);

    public void SetAutomaticChecksEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (AutomaticChecksEnabled == enabled)
        {
            return;
        }

        _settings.Update(settings => settings with
        {
            StoreUpdates = settings.StoreUpdates with
            {
                AutomaticChecksEnabled = enabled
            }
        });
        if (enabled && _started)
        {
            _ = RunEnabledCheckSafelyAsync(_lifetime.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _storeUpdates.UpdateAvailabilityChanged -= OnAvailabilityChanged;
        _lifetime.Cancel();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        _lifetime.Dispose();
        _checkGate.Dispose();
    }

    private async Task RunAutomaticChecksAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(InitialDelayFromLastCheck(), cancellationToken)
            .ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            await CheckCoreAsync(
                    ignoreAutomaticPreference: false,
                    cancellationToken)
                .ConfigureAwait(false);
            await Task.Delay(AutomaticCheckInterval, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task RunAutomaticChecksSafelyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await RunAutomaticChecksAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _log.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.StoreUpdateCheckFailed,
                "StoreUpdate",
                "Automatic Microsoft Store update loop stopped.",
                properties: new Dictionary<string, object?>
                {
                    ["hresult"] = $"0x{exception.HResult:X8}"
                });
        }
    }

    private async Task RunEnabledCheckSafelyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(InitialCheckDelay, cancellationToken)
                .ConfigureAwait(false);
            await CheckCoreAsync(
                    ignoreAutomaticPreference: false,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _log.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.StoreUpdateCheckFailed,
                "StoreUpdate",
                "Scheduled Microsoft Store update check failed.",
                properties: new Dictionary<string, object?>
                {
                    ["hresult"] = $"0x{exception.HResult:X8}"
                });
        }
    }

    private async Task<StoreUpdateSnapshot> CheckCoreAsync(
        bool ignoreAutomaticPreference,
        CancellationToken cancellationToken)
    {
        if (!ignoreAutomaticPreference && !AutomaticChecksEnabled)
        {
            return Current;
        }

        await _checkGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var lastCheck = _settings.Current.StoreUpdates.LastCheckUtc;
            if (!ignoreAutomaticPreference &&
                lastCheck is { } timestamp &&
                DateTimeOffset.UtcNow - timestamp is { } elapsed &&
                elapsed >= TimeSpan.Zero &&
                elapsed < MinimumQueryInterval)
            {
                return Current;
            }

            var result = await _storeUpdates
                .CheckAsync(cancellationToken)
                .ConfigureAwait(false);
            if (result.Status == StoreUpdateStatus.Checking)
            {
                return result;
            }

            await RunOnUiThreadAsync(() =>
            {
                var availableVersion = result.AvailableVersion?.ToString(4);
                var shouldNotify =
                    result.Status == StoreUpdateStatus.UpdateAvailable &&
                    availableVersion is not null &&
                    !string.Equals(
                        _settings.Current.StoreUpdates.LastNotifiedVersion,
                        availableVersion,
                        StringComparison.Ordinal);
                _settings.Update(settings => settings with
                {
                    StoreUpdates = settings.StoreUpdates with
                    {
                        LastCheckUtc = result.CheckedAtUtc ?? DateTimeOffset.UtcNow,
                        LastNotifiedVersion = shouldNotify
                            ? availableVersion
                            : settings.StoreUpdates.LastNotifiedVersion
                    }
                });
                if (shouldNotify)
                {
                    _module.PublishAvailable(result);
                }
            }).ConfigureAwait(false);
            return result;
        }
        finally
        {
            _checkGate.Release();
        }
    }

    private TimeSpan InitialDelayFromLastCheck()
    {
        if (_settings.Current.StoreUpdates.LastCheckUtc is not { } lastCheck)
        {
            return InitialCheckDelay;
        }

        var elapsed = DateTimeOffset.UtcNow - lastCheck;
        if (elapsed < TimeSpan.Zero)
        {
            return InitialCheckDelay;
        }

        var minimumRemaining = MinimumQueryInterval - elapsed;
        return minimumRemaining > InitialCheckDelay
            ? minimumRemaining
            : InitialCheckDelay;
    }

    private Task RunOnUiThreadAsync(Action action)
    {
        if (_dispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher.TryEnqueue(() =>
            {
                try
                {
                    action();
                    completion.SetResult();
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }))
        {
            completion.SetException(
                new InvalidOperationException("The UI dispatcher is unavailable."));
        }
        return completion.Task;
    }

    private void OnAvailabilityChanged(
        object? sender,
        StoreUpdateSnapshot snapshot) =>
        UpdateAvailabilityChanged?.Invoke(this, snapshot);
}
