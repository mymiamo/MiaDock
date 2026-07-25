using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;

namespace MiaDock.Modules.DeviceStatus.ViewModels;

public sealed partial class NetworkModuleViewModel : ObservableObject, IDisposable
{
    private readonly INetworkStatusService _service;

    public NetworkModuleViewModel(INetworkStatusService service)
    {
        _service = service;
        _snapshot = service.Current;
        _service.SnapshotChanged += OnSnapshotChanged;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectivityText))]
    [NotifyPropertyChangedFor(nameof(ConnectionText))]
    [NotifyPropertyChangedFor(nameof(CostText))]
    [NotifyPropertyChangedFor(nameof(DownloadText))]
    [NotifyPropertyChangedFor(nameof(UploadText))]
    [NotifyPropertyChangedFor(nameof(NetworkGlyph))]
    private NetworkStatusSnapshot _snapshot;

    public string ConnectivityText => Snapshot.Connectivity switch
    {
        NetworkConnectivityKind.Internet => "İnternete bağlı",
        NetworkConnectivityKind.ConstrainedInternet => "Sınırlı internet",
        NetworkConnectivityKind.LocalAccess => "Yalnızca yerel ağ",
        _ => "Çevrimdışı"
    };

    public string ConnectionText => Snapshot.ConnectionKind switch
    {
        NetworkConnectionKind.WiFi => "Wi-Fi",
        NetworkConnectionKind.Ethernet => "Ethernet",
        NetworkConnectionKind.Cellular => "Mobil ağ",
        NetworkConnectionKind.Other => "Diğer bağlantı",
        _ => "Bağlantı yok"
    };

    public string CostText => Snapshot.IsMetered ? "Tarifeli bağlantı" : "Tarifesiz bağlantı";

    public string DownloadText => FormatRate(Snapshot.DownloadBytesPerSecond);

    public string UploadText => FormatRate(Snapshot.UploadBytesPerSecond);

    public string NetworkGlyph => Snapshot.ConnectionKind switch
    {
        NetworkConnectionKind.WiFi => "\uE701",
        NetworkConnectionKind.Ethernet => "\uE839",
        _ => "\uE774"
    };

    public void SetExpandedActive(bool active) => _service.SetThroughputSamplingEnabled(active);

    private void OnSnapshotChanged(object? sender, NetworkStatusSnapshot snapshot) => Snapshot = snapshot;

    private static string FormatRate(double? bytesPerSecond)
    {
        if (bytesPerSecond is null)
        {
            return "—";
        }

        var value = Math.Max(0, bytesPerSecond.Value);
        return value >= 1024 * 1024
            ? $"{value / (1024 * 1024):0.0} MB/sn"
            : $"{value / 1024:0} KB/sn";
    }

    public void Dispose()
    {
        _service.SetThroughputSamplingEnabled(false);
        _service.SnapshotChanged -= OnSnapshotChanged;
    }
}
