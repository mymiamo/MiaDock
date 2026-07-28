using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Core.Localization;
using MiaDock.Modules.Transfers.Models;
using MiaDock.Modules.Transfers.Services;

namespace MiaDock.Modules.Transfers.ViewModels;

public sealed class TransferModuleViewModel : ObservableObject, IDisposable
{
    private readonly ITransferStateService _service;
    private readonly ILocalizationService? _localization;
    private IReadOnlyList<TransferSnapshot> _activeTransfers;

    public TransferModuleViewModel(ITransferStateService service, ILocalizationService? localization = null)
    {
        _service = service;
        _localization = localization;
        _activeTransfers = service.ActiveTransfers;
        _service.TransfersChanged += OnTransfersChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
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
    public string Title => Current?.SafeDisplayName ?? Text("Transfer.Title", "Dosya aktarımları");
    public string StatusText => Current is null
        ? Text("Transfer.None", "Etkin aktarım yok")
        : StatusToText(Current.Status);
    public double Progress => Current?.Progress ?? 0;
    public string ValueText => Current is null
        ? string.Empty
        : $"{FormatBytes(Current.TransferredBytes)} / {(Current.TotalBytes > 0 ? FormatBytes(Current.TotalBytes) : "?")}";
    public string ActiveCountText => ActiveTransfers.Count switch
    {
        0 => Text("Transfer.Waiting", "Aktarım bekleniyor"),
        1 => Text("Transfer.Active.One", "1 etkin aktarım"),
        _ => Text("Transfer.Active.Many", "{0} etkin aktarım", ActiveTransfers.Count)
    };

    public string StatusToText(TransferStatus status) => status switch
    {
        TransferStatus.Queued => Text("Transfer.Queued", "Sırada"),
        TransferStatus.Running => Text("Transfer.Running", "Aktarılıyor"),
        TransferStatus.Paused => Text("Transfer.Paused", "Duraklatıldı"),
        TransferStatus.Waiting => Text("Transfer.ProviderWaiting", "Sağlayıcı bekleniyor"),
        TransferStatus.Completed => Text("Transfer.Completed", "Tamamlandı"),
        TransferStatus.Failed => Text("Transfer.Failed", "Başarısız"),
        TransferStatus.Cancelled => Text("Transfer.Cancelled", "İptal edildi"),
        TransferStatus.Disconnected => Text("Transfer.Disconnected", "Bağlantı kesildi"),
        _ => Text("Common.Unknown", "Bilinmiyor")
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

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ActiveCountText));
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
        _service.TransfersChanged -= OnTransfersChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }
}
