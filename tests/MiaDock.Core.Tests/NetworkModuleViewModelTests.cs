using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.DeviceStatus.ViewModels;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class NetworkModuleViewModelTests
{
    [TestMethod]
    public void ThroughputStates_DistinguishSamplingZeroAndUnavailable()
    {
        var service = new FakeNetworkStatusService(Create(NetworkThroughputState.Sampling));
        using var viewModel = new NetworkModuleViewModel(service);

        Assert.AreEqual("Ölçülüyor…", viewModel.DownloadText);

        service.Publish(Create(NetworkThroughputState.Ready) with
        {
            DownloadBytesPerSecond = 0,
            UploadBytesPerSecond = 0
        });
        Assert.AreEqual("0 KB/sn", viewModel.DownloadText);
        Assert.AreEqual("Canlı hız", viewModel.ThroughputStatusText);

        service.Publish(Create(NetworkThroughputState.Unavailable));
        Assert.AreEqual("Hız kullanılamıyor", viewModel.DownloadText);
        Assert.AreEqual("Hız kullanılamıyor", viewModel.ThroughputStatusText);
    }

    [TestMethod]
    public void ExpandedActivation_ControlsSamplingLifecycle()
    {
        var service = new FakeNetworkStatusService(Create(NetworkThroughputState.Inactive));
        using var viewModel = new NetworkModuleViewModel(service);

        viewModel.SetExpandedActive(true);
        viewModel.SetExpandedActive(false);

        CollectionAssert.AreEqual(new[] { true, false }, service.SamplingStates);
    }

    private static NetworkStatusSnapshot Create(NetworkThroughputState throughputState) => new(
        DeviceServiceState.Ready,
        NetworkConnectivityKind.Internet,
        NetworkConnectionKind.Ethernet,
        false,
        Guid.NewGuid(),
        null,
        null,
        throughputState);

    private sealed class FakeNetworkStatusService(NetworkStatusSnapshot current) : INetworkStatusService
    {
        public NetworkStatusSnapshot Current { get; private set; } = current;
        public List<bool> SamplingStates { get; } = [];
        public event EventHandler<NetworkStatusSnapshot>? SnapshotChanged;
        public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public void SetThroughputSamplingEnabled(bool enabled) => SamplingStates.Add(enabled);
        public void Publish(NetworkStatusSnapshot snapshot)
        {
            Current = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        }
        public void Dispose() { }
    }
}
