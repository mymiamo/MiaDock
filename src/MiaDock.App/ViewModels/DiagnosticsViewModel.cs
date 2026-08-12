using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Core.Logging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace MiaDock.App.ViewModels;

public sealed class DiagnosticsViewModel(
    ILogService logService,
    ILogReader logReader) : ObservableObject
{
    private int _fileCount;
    private long _totalBytes;
    private string _statusMessage = "Log bilgileri yükleniyor.";
    private bool _isBusy;

    public ObservableCollection<TechnicalLogEntry> RecentEntries { get; } = [];

    public int FileCount
    {
        get => _fileCount;
        private set
        {
            if (SetProperty(ref _fileCount, value))
            {
                OnPropertyChanged(nameof(FileCountText));
            }
        }
    }

    public string FileCountText => $"Dosya: {FileCount}";

    public string TotalSizeText => TotalBytes < 1024
        ? $"{TotalBytes} B"
        : TotalBytes < 1024 * 1024
            ? $"{TotalBytes / 1024d:F1} KB"
            : $"{TotalBytes / 1024d / 1024d:F1} MB";

    public string StorageSizeText => $"Boyut: {TotalSizeText}";

    public long TotalBytes
    {
        get => _totalBytes;
        private set
        {
            if (SetProperty(ref _totalBytes, value))
            {
                OnPropertyChanged(nameof(TotalSizeText));
                OnPropertyChanged(nameof(StorageSizeText));
            }
        }
    }

    public string LogDirectoryPath => logService.LogDirectoryPath;

    public string RuntimeSummaryText
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
            return $"MiaDock {version} · {RuntimeInformation.OSDescription} · {RuntimeInformation.ProcessArchitecture}";
        }
    }

    public string SessionSummaryText => RecentEntries.FirstOrDefault() is { } latest
        ? $"Son oturum: {latest.SessionId} · Düşürülen kayıt: {logService.DroppedEntryCount}"
        : $"Düşürülen kayıt: {logService.DroppedEntryCount}";

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var entries = await logReader.ReadLatestAsync(250, cancellationToken);
            var storage = await logReader.GetStorageInfoAsync(cancellationToken);
            RecentEntries.Clear();
            foreach (var entry in entries)
            {
                RecentEntries.Add(entry);
            }

            OnPropertyChanged(nameof(SessionSummaryText));

            FileCount = storage.FileCount;
            TotalBytes = storage.TotalBytes;
            StatusMessage = logService.LastFailure is null
                ? $"{entries.Count} güvenli teknik kayıt gösteriliyor. ZIP; çalışma ortamı, olay özeti ve son 250 olay zaman çizelgesini içerir."
                : $"Son log yazma işlemi tamamlanamadı: {logService.LastFailure.GetType().Name} (0x{logService.LastFailure.HResult:X8}).";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            StatusMessage = "Log kayıtları okunamadı.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            await logReader.ClearAsync(cancellationToken);
            RecentEntries.Clear();
            FileCount = 0;
            TotalBytes = 0;
            OnPropertyChanged(nameof(SessionSummaryText));
            StatusMessage = "Yerel loglar temizlendi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ReportExportResult(bool exported) =>
        StatusMessage = exported ? "Loglar ZIP olarak dışa aktarıldı." : "Dışa aktarma iptal edildi.";

    public void ReportFileOperationFailure() => StatusMessage = "Dosya işlemi tamamlanamadı.";
}
