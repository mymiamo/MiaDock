using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using Windows.UI;

namespace MiaDock.App.ViewModels;

public sealed class IdleDashboardViewModel : ObservableObject, IDisposable
{
    private readonly IPowerStatusService _powerService;
    private readonly INetworkStatusService _networkService;
    private readonly IBluetoothStatusService _bluetoothService;
    private BatteryStatusSnapshot _battery;
    private NetworkStatusSnapshot _network;
    private BluetoothStatusSnapshot _bluetooth;

    public IdleDashboardViewModel(
        IPowerStatusService powerService,
        INetworkStatusService networkService,
        IBluetoothStatusService bluetoothService)
    {
        _powerService = powerService;
        _networkService = networkService;
        _bluetoothService = bluetoothService;
        _battery = powerService.Current;
        _network = networkService.Current;
        _bluetooth = bluetoothService.Current;

        _powerService.SnapshotChanged += OnBatteryChanged;
        _networkService.SnapshotChanged += OnNetworkChanged;
        _bluetoothService.SnapshotChanged += OnBluetoothChanged;
    }

    public string BatteryGlyph => _battery.IsCharging ? "\uE83E" : "\uE850";

    public string BatteryText => $"{_battery.ChargePercent}%";

    public Visibility BatteryVisibility => _battery.IsBatteryPresent
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string BatteryStatus => !_battery.IsBatteryPresent
        ? "Pil bulunmuyor"
        : _battery.IsCharging
            ? $"Pil yüzde {_battery.ChargePercent}, şarj oluyor"
            : _battery.IsEnergySaverOn
                ? $"Pil yüzde {_battery.ChargePercent}, enerji tasarrufu açık"
                : $"Pil yüzde {_battery.ChargePercent}";

    public string NetworkGlyph => _network.ConnectionKind switch
    {
        NetworkConnectionKind.WiFi => "\uE701",
        NetworkConnectionKind.Ethernet => "\uE839",
        NetworkConnectionKind.Cellular => "\uEC05",
        _ => "\uE774"
    };

    public double NetworkOpacity => _network.Connectivity == NetworkConnectivityKind.Offline ? 0.48 : 1;

    public Brush NetworkStatusBrush => _network.Connectivity switch
    {
        NetworkConnectivityKind.Internet =>
            new SolidColorBrush(Color.FromArgb(255, 74, 222, 128)),
        NetworkConnectivityKind.ConstrainedInternet or NetworkConnectivityKind.LocalAccess =>
            new SolidColorBrush(Color.FromArgb(255, 245, 158, 11)),
        _ => new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
    };

    public string NetworkStatus => _network.Connectivity switch
    {
        NetworkConnectivityKind.Internet => _network.ConnectionKind switch
        {
            NetworkConnectionKind.WiFi => _network.IsMetered ? "Wi-Fi, tarifeli bağlantı" : "Wi-Fi bağlı",
            NetworkConnectionKind.Ethernet => _network.IsMetered ? "Ethernet, tarifeli bağlantı" : "Ethernet bağlı",
            NetworkConnectionKind.Cellular => "Mobil ağa bağlı",
            _ => "İnternete bağlı"
        },
        NetworkConnectivityKind.ConstrainedInternet => "Sınırlı internet bağlantısı",
        NetworkConnectivityKind.LocalAccess => "Yalnızca yerel ağ bağlantısı",
        _ => "Çevrimdışı"
    };

    public int ConnectedBluetoothCount => _bluetooth.Devices.Count(device => device.IsConnected);

    public string BluetoothCountText => ConnectedBluetoothCount.ToString();

    public Visibility BluetoothVisibility => ConnectedBluetoothCount > 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string BluetoothStatus => ConnectedBluetoothCount switch
    {
        0 => "Bağlı Bluetooth cihazı yok",
        1 => "Bir Bluetooth cihazı bağlı",
        var count => $"{count} Bluetooth cihazı bağlı"
    };

    public string StatusSummary
    {
        get
        {
            var parts = new List<string> { NetworkStatus };
            if (_battery.IsBatteryPresent)
            {
                parts.Add(BatteryStatus);
            }

            if (ConnectedBluetoothCount > 0)
            {
                parts.Add(BluetoothStatus);
            }

            return string.Join(", ", parts);
        }
    }

    private void OnBatteryChanged(object? sender, BatteryStatusSnapshot snapshot)
    {
        _battery = snapshot;
        OnPropertyChanged(nameof(BatteryGlyph));
        OnPropertyChanged(nameof(BatteryText));
        OnPropertyChanged(nameof(BatteryVisibility));
        OnPropertyChanged(nameof(BatteryStatus));
        OnPropertyChanged(nameof(StatusSummary));
    }

    private void OnNetworkChanged(object? sender, NetworkStatusSnapshot snapshot)
    {
        _network = snapshot;
        OnPropertyChanged(nameof(NetworkGlyph));
        OnPropertyChanged(nameof(NetworkOpacity));
        OnPropertyChanged(nameof(NetworkStatusBrush));
        OnPropertyChanged(nameof(NetworkStatus));
        OnPropertyChanged(nameof(StatusSummary));
    }

    private void OnBluetoothChanged(object? sender, BluetoothStatusSnapshot snapshot)
    {
        _bluetooth = snapshot;
        OnPropertyChanged(nameof(ConnectedBluetoothCount));
        OnPropertyChanged(nameof(BluetoothCountText));
        OnPropertyChanged(nameof(BluetoothVisibility));
        OnPropertyChanged(nameof(BluetoothStatus));
        OnPropertyChanged(nameof(StatusSummary));
    }

    public void Dispose()
    {
        _powerService.SnapshotChanged -= OnBatteryChanged;
        _networkService.SnapshotChanged -= OnNetworkChanged;
        _bluetoothService.SnapshotChanged -= OnBluetoothChanged;
    }
}
