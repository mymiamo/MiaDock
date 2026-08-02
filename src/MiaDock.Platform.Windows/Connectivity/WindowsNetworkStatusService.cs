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
    private readonly INetworkInterfaceCounterReader _counterReader;
    private readonly TimeSpan _samplingInterval;
    private readonly object _gate = new();
    private readonly NetworkRateCalculator _rateCalculator = new();
    private CancellationTokenSource? _samplingCancellation;
    private Task _samplingTask = Task.CompletedTask;
    private bool _started;
    private bool _samplingRequested;
    private bool _counterFailureLogged;
    private bool _disposed;

    public WindowsNetworkStatusService(IUiDispatcher dispatcher, ILogService? log = null)
        : this(
            dispatcher,
            new NetworkInterfaceCounterReader(),
            TimeSpan.FromSeconds(1),
            log)
    {
    }

    internal WindowsNetworkStatusService(
        IUiDispatcher dispatcher,
        INetworkInterfaceCounterReader counterReader,
        TimeSpan samplingInterval,
        ILogService? log = null)
    {
        _dispatcher = dispatcher;
        _counterReader = counterReader;
        _samplingInterval = samplingInterval;
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
        StartSamplingIfRequested();
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
        var publishState = false;
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
                _counterFailureLogged = false;
                publishState = Current.ThroughputState != NetworkThroughputState.Inactive ||
                    Current.DownloadBytesPerSecond is not null || Current.UploadBytesPerSecond is not null;
                if (cancellation is not null)
                {
                    _ = task.ContinueWith(_ => cancellation.Dispose(), TaskScheduler.Default);
                }
            }
            else
            {
                publishState = true;
                StartSamplingUnderLock();
            }
        }

        if (publishState)
        {
            Publish(Current with
            {
                DownloadBytesPerSecond = null,
                UploadBytesPerSecond = null,
                ThroughputState = enabled
                    ? NetworkThroughputState.Sampling
                    : NetworkThroughputState.Inactive
            });
        }
    }

    private void StartSamplingIfRequested()
    {
        lock (_gate)
        {
            StartSamplingUnderLock();
        }
    }

    private void StartSamplingUnderLock()
    {
        if (!_samplingRequested || !_started || _samplingCancellation is not null)
        {
            return;
        }

        _samplingCancellation = new CancellationTokenSource();
        _samplingTask = RunSamplingAsync(_samplingCancellation.Token);
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
            if (previous.AdapterId != adapterId)
            {
                _rateCalculator.Reset();
                _counterFailureLogged = false;
            }
            Publish(new NetworkStatusSnapshot(
                DeviceServiceState.Ready,
                connectivity,
                kind,
                metered,
                adapterId,
                previous.AdapterId == adapterId ? previous.DownloadBytesPerSecond : null,
                previous.AdapterId == adapterId ? previous.UploadBytesPerSecond : null,
                previous.AdapterId == adapterId
                    ? previous.ThroughputState
                    : _samplingRequested ? NetworkThroughputState.Sampling : NetworkThroughputState.Inactive));
        }
        catch (Exception)
        {
            Publish(Current with { State = DeviceServiceState.Faulted });
        }
    }

    private async Task RunSamplingAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(_samplingInterval);
        try
        {
            SampleThroughput();
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                SampleThroughput();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void SampleThroughput()
    {
        var adapterId = Current.AdapterId;
        if (adapterId is null ||
            !_counterReader.TryRead(adapterId.Value, out var received, out var sent))
        {
            _rateCalculator.Reset();
            Publish(Current with
            {
                DownloadBytesPerSecond = null,
                UploadBytesPerSecond = null,
                ThroughputState = NetworkThroughputState.Unavailable
            });
            if (!_counterFailureLogged)
            {
                _counterFailureLogged = true;
                _log?.Write(
                    TechnicalLogLevel.Warning,
                    TechnicalEventIds.NetworkCountersUnavailable,
                    "DeviceStatus",
                    "Network throughput counters are unavailable.");
            }
            return;
        }

        _counterFailureLogged = false;
        var rate = _rateCalculator.Add(
            new NetworkCounterSnapshot(received, sent, DateTimeOffset.UtcNow));
        if (rate is { } value)
        {
            Publish(Current with
            {
                DownloadBytesPerSecond = value.Download,
                UploadBytesPerSecond = value.Upload,
                ThroughputState = NetworkThroughputState.Ready
            });
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
