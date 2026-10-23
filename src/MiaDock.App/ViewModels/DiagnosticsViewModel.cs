using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Core.Logging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using MiaDock.Core.Diagnostics;
using MiaDock.App.Services;

namespace MiaDock.App.ViewModels;

public sealed class DiagnosticsViewModel(
    ILogService logService,
    ILogReader logReader,
    ApplicationPerformanceMonitor? performance = null,
    IAppLocalizationService? localization = null) : ObservableObject
{
    private int _fileCount;
    private long _totalBytes;
    private string _statusMessage = "Log bilgileri yükleniyor.";
    private bool _isBusy;
    private string _performanceSummaryText = string.Empty;
    private string _animationSummaryText = string.Empty;
    private bool _canStartAnimationMeasurement = true;
    public string AnimationSummaryText { get => _animationSummaryText; private set => SetProperty(ref _animationSummaryText, value); }
    public bool CanStartAnimationMeasurement { get => _canStartAnimationMeasurement; private set => SetProperty(ref _canStartAnimationMeasurement, value); }
    public void StartAnimationMeasurement() { performance?.StartAnimationMeasurement(); RefreshPerformance(); }
    public void StopAnimationMeasurement() { performance?.StopAnimationMeasurement(); RefreshPerformance(); }

    public string PerformanceSummaryText { get => _performanceSummaryText; private set => SetProperty(ref _performanceSummaryText, value); }

    public void RefreshPerformance()
    {
        if (performance is null) return;
        string L(string tr, string en) => localization?.Text(tr, en) ?? tr;
        var snapshot = performance.Snapshot();
        var completedDurations = snapshot.Animation?.Transitions.Where(t => !t.Cancelled).Select(t => t.Milliseconds).ToArray() ?? [];
        var completedTiming = completedDurations.Length == 0 ? L("ölçüm yok", "no measurements") : $"{completedDurations.Average():F1} ms";
        CanStartAnimationMeasurement = snapshot.Animation?.IsRunning != true;
        AnimationSummaryText = snapshot.Animation is { } animation
            ? $"{L("UI akıcılık ölçümü", "UI smoothness measurement")}: {animation.ElapsedSeconds:F0}/60 s · " +
              (animation.IsRunning ? L("ölçülüyor", "recording") : L("durdu", "stopped")) + Environment.NewLine +
              $"p95 {animation.P95FrameIntervalMilliseconds:F1} ms · p99 {animation.P99FrameIntervalMilliseconds:F1} ms · " +
              $"{animation.DelayedFrames} {L("geciken aralık (>25 ms)", "delayed intervals (>25 ms)")} · " +
              $"{animation.FrameIntervals} {L("örnek", "samples")} · {animation.DroppedFrameSamples} {L("eski örnek çıkarıldı", "older samples evicted")}" + Environment.NewLine +
              $"{L("Geçiş", "Transitions")}: {animation.CompletedTransitions} {L("tamamlandı", "completed")}, {animation.CancelledTransitions} {L("iptal", "cancelled")} · " +
              $"{L("tamamlanan ortalama", "completed mean")} {completedTiming}. " +
              L("UI kare aralığıdır; GPU sunum FPS'i değildir. Geçiş ayrıntıları JSON dışa aktarımındadır.", "UI callback intervals, not GPU presentation FPS. Transition details are included in the JSON export.")
            : L("60 saniyelik UI ölçümünü başlatıp dock ile etkileşime geçin. Otomatik durur; ayarlardan ayrılınca devam eder. UI kare aralığı GPU sunum FPS'i değildir.",
                "Start a 60-second UI measurement and interact with the dock. It stops automatically and continues after leaving settings. UI callback intervals are not GPU presentation FPS.");
        var lines = new List<string>
        {
            snapshot.DockReadyMilliseconds is { } milliseconds
                ? $"{L("Başlangıç (dock hazır)", "Startup (dock ready)")}: {milliseconds:F0} ms"
                : L("Başlangıç: dock henüz gösterilmedi.", "Startup: dock has not been shown yet.")
        };
        foreach (var scenario in snapshot.Scenarios)
        {
            var name = scenario.Scenario switch { PerformanceScenario.Media => L("Medya", "Media"),
                PerformanceScenario.Spectrum => "Spectrum", _ => L("Boşta", "Idle") };
            lines.Add(scenario.SampleCount == 0 ? $"{name}: {L("ölçüm bekleniyor", "awaiting measurements")}" :
                $"{name}: CPU {scenario.MeanCpuPercent:F2}% · {L("tepe", "peak")} {scenario.PeakCpuPercent:F2}% · RAM {scenario.LatestWorkingSetBytes / 1048576d:F1} MB · {L("özel bellek", "private memory")} {scenario.LatestPrivateBytes / 1048576d:F1} MB · {scenario.SampleCount} {L("örnek", "samples")}");
        }
        lines.Add(L("5 saniyede bir ölçülür. CPU yüzdesi tüm işlemciye göredir; son 15 dakikaya kadar örnekler tutulur.",
            "Sampled every 5 seconds. CPU is relative to the whole processor; up to 15 minutes of samples are retained."));
        PerformanceSummaryText = string.Join(Environment.NewLine, lines);
    }

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
        RefreshPerformance();
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
