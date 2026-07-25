using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Modules.Transfers.Models;
using MiaDock.Modules.Transfers.Services;

namespace MiaDock.Modules.Transfers.ViewModels;

public sealed class TransferModuleViewModel : ObservableObject, IDisposable
{
    private readonly ITransferStateService _service;
    private IReadOnlyList<TransferSnapshot> _activeTransfers;

    public TransferModuleViewModel(ITransferStateService service)
    {
        _service = service;
        _activeTransfers = service.ActiveTransfers;
        _service.TransfersChanged += OnTransfersChanged;
    }

    public IReadOnlyList<TransferSnapshot> ActiveTransfers
    {
        get => _activeTransfers;
        private set
        {
            if (SetProperty(ref _activeTransfers, value))
            {
                OnPropertyChanged(nameof(Current));
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(Progress));
                OnPropertyChanged(nameof(ValueText));
                OnPropertyChanged(nameof(ActiveCountText));
                OnPropertyChanged(nameof(HasActiveTransfer));
            }
        }
    }

    public TransferSnapshot? Current => ActiveTransfers.FirstOrDefault();
    public bool HasActiveTransfer => Current is not null;
    public string Title => Current?.SafeDisplayName ?? "Dosya aktarımları";
    public string StatusText => Current is null ? "Etkin aktarım yok" : StatusToText(Current.Status);
    public double Progress => Current?.Progress ?? 0;
    public string ValueText => Current is null
        ? string.Empty
        : $"{FormatBytes(Current.TransferredBytes)} / {(Current.TotalBytes > 0 ? FormatBytes(Current.TotalBytes) : "?")}";
    public string ActiveCountText => ActiveTransfers.Count switch
    {
        0 => "Aktarım bekleniyor",
        1 => "1 etkin aktarım",
        _ => $"{ActiveTransfers.Count} etkin aktarım"
    };

    public void Dispose() => _service.TransfersChanged -= OnTransfersChanged;

    public static string StatusToText(TransferStatus status) => status switch
    {
        TransferStatus.Queued => "Sırada",
        TransferStatus.Running => "Aktarılıyor",
        TransferStatus.Paused => "Duraklatıldı",
        TransferStatus.Waiting => "Sağlayıcı bekleniyor",
        TransferStatus.Completed => "Tamamlandı",
        TransferStatus.Failed => "Başarısız",
        TransferStatus.Cancelled => "İptal edildi",
        TransferStatus.Disconnected => "Bağlantı kesildi",
        _ => "Bilinmiyor"
    };

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(0, (double)bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }

    private void OnTransfersChanged(object? sender, IReadOnlyList<TransferSnapshot> transfers) =>
        ActiveTransfers = transfers;
}
