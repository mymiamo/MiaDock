using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Core.Localization;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;

namespace MiaDock.Modules.DeviceStatus.ViewModels;

public sealed partial class NetworkModuleViewModel : ObservableObject, IDisposable
{
    private readonly INetworkStatusService _service;
    private readonly ILocalizationService? _localization;

    public NetworkModuleViewModel(INetworkStatusService service, ILocalizationService? localization = null)
    {
        _service = service;
        _localization = localization;
        _snapshot = service.Current;
        _service.SnapshotChanged += OnSnapshotChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectivityText))]
    [NotifyPropertyChangedFor(nameof(ConnectionText))]
    [NotifyPropertyChangedFor(nameof(CostText))]
    [NotifyPropertyChangedFor(nameof(DownloadText))]
    [NotifyPropertyChangedFor(nameof(UploadText))]
    [NotifyPropertyChangedFor(nameof(NetworkGlyph))]
    [NotifyPropertyChangedFor(nameof(ThroughputStatusText))]
    private NetworkStatusSnapshot _snapshot;

    public string ConnectivityText => Snapshot.Connectivity switch
    {
        NetworkConnectivityKind.Internet => Text("Network.Internet", "İnternete bağlı"),
        NetworkConnectivityKind.ConstrainedInternet => Text("Network.Constrained", "Sınırlı internet"),
        NetworkConnectivityKind.LocalAccess => Text("Network.Local", "Yalnızca yerel ağ"),
        _ => Text("Network.Offline", "Çevrimdışı")
    };

    public string ConnectionText => Snapshot.ConnectionKind switch
    {
        NetworkConnectionKind.WiFi => "Wi-Fi",
        NetworkConnectionKind.Ethernet => "Ethernet",
        NetworkConnectionKind.Cellular => Text("Network.Cellular", "Mobil ağ"),
        NetworkConnectionKind.Other => Text("Network.Other", "Diğer bağlantı"),
        _ => Text("Network.None", "Bağlantı yok")
    };

    public string CostText => Snapshot.IsMetered
        ? Text("Network.Metered", "Tarifeli bağlantı")
        : Text("Network.Unmetered", "Tarifesiz bağlantı");

    public string DownloadText => FormatRate(Snapshot.DownloadBytesPerSecond);

    public string UploadText => FormatRate(Snapshot.UploadBytesPerSecond);

    public string ThroughputStatusText => Snapshot.ThroughputState switch
    {
        NetworkThroughputState.Sampling => Text("Network.Throughput.Sampling", "Ölçülüyor…"),
        NetworkThroughputState.Ready => Text("Network.Throughput.Ready", "Canlı hız"),
        NetworkThroughputState.Unavailable => Text("Network.Throughput.Unavailable", "Hız kullanılamıyor"),
        _ => Text("Network.Throughput.Inactive", "Görünüm açıldığında ölçülür")
    };

    public string NetworkGlyph => Snapshot.ConnectionKind switch
    {
        NetworkConnectionKind.WiFi => "\uE701",
        NetworkConnectionKind.Ethernet => "\uE839",
        _ => "\uE774"
    };

    public void SetExpandedActive(bool active) => _service.SetThroughputSamplingEnabled(active);

    private void OnSnapshotChanged(object? sender, NetworkStatusSnapshot snapshot) => Snapshot = snapshot;

    private string FormatRate(double? bytesPerSecond)
    {
        if (Snapshot.ThroughputState == NetworkThroughputState.Sampling)
        {
            return Text("Network.Throughput.Sampling", "Ölçülüyor…");
        }

        if (Snapshot.ThroughputState == NetworkThroughputState.Unavailable)
        {
            return Text("Network.Throughput.Unavailable", "Hız kullanılamıyor");
        }

        if (bytesPerSecond is null)
        {
            return "—";
        }

        var value = Math.Max(0, bytesPerSecond.Value);
        return value >= 1024 * 1024
            ? Text("Network.Rate.Megabytes", "{0:0.0} MB/sn", value / (1024 * 1024))
            : Text("Network.Rate.Kilobytes", "{0:0} KB/sn", value / 1024);
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        OnPropertyChanged(nameof(ConnectivityText));
        OnPropertyChanged(nameof(ConnectionText));
        OnPropertyChanged(nameof(CostText));
        OnPropertyChanged(nameof(DownloadText));
        OnPropertyChanged(nameof(UploadText));
        OnPropertyChanged(nameof(ThroughputStatusText));
    }

    private string Text(string key, string fallback, params object?[] arguments)
    {
        var value = _localization?.Get(key, arguments);
        return value is not null && value != key
            ? value
            : string.Format(fallback, arguments);
    }

    public void Dispose()
    {
        _service.SetThroughputSamplingEnabled(false);
        _service.SnapshotChanged -= OnSnapshotChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }
}
