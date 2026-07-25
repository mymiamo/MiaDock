using MiaDock.Core.Threading;
using MiaDock.Core.Logging;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using Windows.Networking.Connectivity;

namespace MiaDock.Platform.Windows.Connectivity;

public sealed class WindowsNetworkStatusService : INetworkStatusService
{
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogService? _log;
    private readonly object _gate = new();
    private readonly NetworkRateCalculator _rateCalculator = new();
    private CancellationTokenSource? _samplingCancellation;
    private Task _samplingTask = Task.CompletedTask;
    private bool _started;
    private bool _samplingRequested;
    private bool _disposed;

    public WindowsNetworkStatusService(IUiDispatcher dispatcher, ILogService? log = null)
    {
        _dispatcher = dispatcher;
        _log = log;
    }

    public NetworkStatusSnapshot Current { get; private set; } = NetworkStatusSnapshot.Default;
    public event EventHandler<NetworkStatusSnapshot>? SnapshotChanged;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            if (_started) return ValueTask.CompletedTask;
            _started = true;
            NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
        }

        RefreshFromSystem();
        _log?.Write(TechnicalLogLevel.Information, TechnicalEventIds.NetworkStatusReady,
            "DeviceStatus", "Network status service initialized.", properties: new Dictionary<string, object?>
            {
                ["state"] = Current.State.ToString(),
                ["connectivity"] = Current.Connectivity.ToString()
            });
        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancellationTokenSource? sampling;
        Task task;
        lock (_gate)
        {
            if (!_started) return;
            _started = false;
            NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
            sampling = _samplingCancellation;
            task = _samplingTask;
            _samplingCancellation = null;
            _samplingTask = Task.CompletedTask;
            _rateCalculator.Reset();
        }

        sampling?.Cancel();
        try { await task.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        sampling?.Dispose();
        Publish(NetworkStatusSnapshot.Default);
    }

    public void SetThroughputSamplingEnabled(bool enabled)
    {
        lock (_gate)
        {
            if (_samplingRequested == enabled) return;
            _samplingRequested = enabled;
            if (!enabled)
            {
                var cancellation = _samplingCancellation;
                var task = _samplingTask;
                cancellation?.Cancel();
                _samplingCancellation = null;
                _samplingTask = Task.CompletedTask;
                _rateCalculator.Reset();
                if (cancellation is not null)
                {
                    _ = task.ContinueWith(_ => cancellation.Dispose(), TaskScheduler.Default);
                }
                return;
            }

            if (!_started || _samplingCancellation is not null) return;
            _samplingCancellation = new CancellationTokenSource();
            _samplingTask = RunSamplingAsync(_samplingCancellation.Token);
        }
    }

    private void OnNetworkStatusChanged(object sender) => RefreshFromSystem();

    private void RefreshFromSystem()
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            var connectivity = profile?.GetNetworkConnectivityLevel().ToString() switch
            {
                "InternetAccess" => NetworkConnectivityKind.Internet,
                "ConstrainedInternetAccess" => NetworkConnectivityKind.ConstrainedInternet,
                "LocalAccess" => NetworkConnectivityKind.LocalAccess,
                _ => NetworkConnectivityKind.Offline
            };
            var kind = profile is null
                ? NetworkConnectionKind.None
                : profile.IsWlanConnectionProfile
                    ? NetworkConnectionKind.WiFi
                    : profile.IsWwanConnectionProfile
                        ? NetworkConnectionKind.Cellular
                        : profile.NetworkAdapter is not null ? NetworkConnectionKind.Ethernet : NetworkConnectionKind.Other;
            var cost = profile?.GetConnectionCost().NetworkCostType.ToString();
            var metered = cost is "Fixed" or "Variable";
            var adapterId = profile?.NetworkAdapter?.NetworkAdapterId;
            var previous = Current;
            if (previous.AdapterId != adapterId) _rateCalculator.Reset();
            Publish(new NetworkStatusSnapshot(
                DeviceServiceState.Ready,
                connectivity,
                kind,
                metered,
                adapterId,
                previous.AdapterId == adapterId ? previous.DownloadBytesPerSecond : null,
                previous.AdapterId == adapterId ? previous.UploadBytesPerSecond : null));
        }
        catch (Exception)
        {
            Publish(Current with { State = DeviceServiceState.Faulted });
        }
    }

    private async Task RunSamplingAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var adapterId = Current.AdapterId;
                if (adapterId is null || !NetworkInterfaceCounterReader.TryRead(adapterId.Value, out var received, out var sent))
                {
                    _rateCalculator.Reset();
                    Publish(Current with { DownloadBytesPerSecond = null, UploadBytesPerSecond = null });
                    continue;
                }

                var rate = _rateCalculator.Add(new NetworkCounterSnapshot(received, sent, DateTimeOffset.UtcNow));
                if (rate is { } value)
                {
                    Publish(Current with { DownloadBytesPerSecond = value.Download, UploadBytesPerSecond = value.Upload });
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void Publish(NetworkStatusSnapshot snapshot)
    {
        void Apply()
        {
            Current = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        }
        if (_dispatcher.HasThreadAccess) Apply(); else _dispatcher.TryEnqueue(Apply);
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_started) NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
        var sampling = _samplingCancellation;
        var task = _samplingTask;
        sampling?.Cancel();
        _samplingCancellation = null;
        _samplingTask = Task.CompletedTask;
        if (sampling is not null)
        {
            _ = task.ContinueWith(
                _ => sampling.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        _started = false;
        _disposed = true;
    }
}
