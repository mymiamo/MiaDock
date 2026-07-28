using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.Core.Localization;
using MiaDock.Core.Threading;
using MiaDock.Modules.Time.Models;
using MiaDock.Modules.Time.Services;

namespace MiaDock.Modules.Time.ViewModels;

public sealed partial class TimeToolsViewModel : ObservableObject, IDisposable
{
    private readonly ITimeToolsService _service;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILocalizationService? _localization;
    private readonly object _snapshotSync = new();
    private TimeToolsSnapshot _pendingSnapshot;
    private long _pendingSnapshotVersion;
    private int _snapshotDispatchPending;
    private bool _disposed;

    public TimeToolsViewModel(
        ITimeToolsService service,
        IUiDispatcher? dispatcher = null,
        ILocalizationService? localization = null)
    {
        _service = service;
        _dispatcher = dispatcher ?? ImmediateUiDispatcher.Instance;
        _localization = localization;
        _current = service.Current;
        _pendingSnapshot = _current;
        _service.SnapshotChanged += OnSnapshotChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimerText))]
    [NotifyPropertyChangedFor(nameof(TimerStatusText))]
    [NotifyPropertyChangedFor(nameof(TimerProgressPercent))]
    [NotifyPropertyChangedFor(nameof(TimerPrimaryText))]
    [NotifyPropertyChangedFor(nameof(TimerSecondaryText))]
    [NotifyPropertyChangedFor(nameof(TimerPrimaryGlyph))]
    [NotifyPropertyChangedFor(nameof(StopwatchText))]
    [NotifyPropertyChangedFor(nameof(StopwatchPrimaryText))]
    [NotifyPropertyChangedFor(nameof(CompactTimeText))]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(CompactPrimaryText))]
    [NotifyPropertyChangedFor(nameof(CompactPrimaryGlyph))]
    [NotifyPropertyChangedFor(nameof(CompactSecondaryText))]
    [NotifyPropertyChangedFor(nameof(IsTimerCompleted))]
    [NotifyCanExecuteChangedFor(nameof(TimerPrimaryCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelTimerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopwatchPrimaryCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddLapCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetStopwatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompactPrimaryCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompactSecondaryCommand))]
    private TimeToolsSnapshot _current;

    [ObservableProperty] private double _customHours;
    [ObservableProperty] private double _customMinutes = 5;
    [ObservableProperty] private double _customSeconds;
    [ObservableProperty] private int _selectedToolIndex;

    public IReadOnlyList<int> PresetMinutes { get; } = [5, 10, 15, 25, 30, 45, 60];

    public string TimerText => FormatDuration(Current.TimerRemaining);

    public string TimerStatusText => Current.TimerState switch
    {
        TimerRunState.Running => Text("Timer.Running", "Zamanlayıcı çalışıyor"),
        TimerRunState.Paused => Text("Timer.Paused", "Zamanlayıcı duraklatıldı"),
        TimerRunState.Completed => Text("Timer.Completed", "Süre doldu"),
        _ => Text("Timer.SelectDuration", "Bir süre seçin")
    };

    public double TimerProgressPercent => Current.TimerProgress * 100;

    public string TimerPrimaryText => Current.TimerState switch
    {
        TimerRunState.Running => Text("Common.Pause", "Duraklat"),
        TimerRunState.Paused => Text("Common.Resume", "Devam et"),
        _ => Text("Common.Start", "Başlat")
    };

    public string TimerPrimaryGlyph => Current.TimerState == TimerRunState.Running ? "\uE769" : "\uE768";

    public string TimerSecondaryText => Current.TimerState == TimerRunState.Completed
        ? Text("Timer.StopAlarm", "Alarmı sustur")
        : Text("Common.Cancel", "İptal");

    public string StopwatchText => Current.StopwatchElapsed.ToString(@"hh\:mm\:ss\.f");

    public string StopwatchPrimaryText => Current.IsStopwatchRunning
        ? Text("Common.Pause", "Duraklat")
        : Text("Common.Start", "Başlat");

    public string CompactTimeText => Current.TimerState is TimerRunState.Running or TimerRunState.Paused
        ? TimerText
        : HasStopwatchActivity
            ? Current.StopwatchElapsed.ToString(@"hh\:mm\:ss")
            : "00:00";

    public string CompactStatusText => Current.TimerState switch
    {
        TimerRunState.Running or TimerRunState.Paused or TimerRunState.Completed => TimerStatusText,
        _ when Current.IsStopwatchRunning => Text("Timer.StopwatchRunning", "Kronometre çalışıyor"),
        _ when HasStopwatchActivity => Text("Timer.StopwatchPaused", "Kronometre duraklatıldı"),
        _ => Text("Timer.Tools", "Zaman araçları")
    };

    public bool IsTimerCompleted => Current.TimerState == TimerRunState.Completed;

    public string CompactPrimaryText => Current.TimerState switch
    {
        TimerRunState.Running => Text("Common.Pause", "Duraklat"),
        TimerRunState.Paused => Text("Common.Resume", "Devam et"),
        _ when Current.IsStopwatchRunning => Text("Common.Pause", "Duraklat"),
        _ when HasStopwatchActivity => Text("Common.Resume", "Devam et"),
        _ => Text("Common.Start", "Başlat")
    };

    public string CompactPrimaryGlyph =>
        Current.TimerState == TimerRunState.Running || Current.IsStopwatchRunning
            ? "\uE769"
            : "\uE768";

    public string CompactSecondaryText =>
        Current.TimerState == TimerRunState.Completed
            ? Text("Timer.StopAlarm", "Alarmı sustur")
            : Current.TimerState != TimerRunState.Idle
            ? Text("Timer.Cancel", "Zamanlayıcıyı iptal et")
            : Text("Timer.ResetStopwatch", "Kronometreyi sıfırla");

    private bool HasStopwatchActivity =>
        Current.IsStopwatchRunning || Current.StopwatchElapsed > TimeSpan.Zero || Current.Laps.Count > 0;

    public IReadOnlyList<string> LapTexts => Current.Laps
        .Select((lap, index) => Text("Timer.Lap", "Tur {0}  {1}", index + 1, lap.ToString(@"hh\:mm\:ss\.f")))
        .Reverse()
        .ToArray();

    [RelayCommand]
    private void StartPreset(string minutes)
    {
        if (int.TryParse(minutes, out var value) && value is > 0 and <= 99 * 60)
        {
            _service.StartTimer(TimeSpan.FromMinutes(value));
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseTimerPrimary))]
    private void TimerPrimary()
    {
        switch (Current.TimerState)
        {
            case TimerRunState.Running:
                _service.PauseTimer();
                break;
            case TimerRunState.Paused:
                _service.ResumeTimer();
                break;
            default:
                var duration = TimeSpan.FromHours(Math.Clamp(CustomHours, 0, 99)) +
                               TimeSpan.FromMinutes(Math.Clamp(CustomMinutes, 0, 59)) +
                               TimeSpan.FromSeconds(Math.Clamp(CustomSeconds, 0, 59));
                _service.StartTimer(duration);
                break;
        }
    }

    private bool CanUseTimerPrimary() => Current.TimerState is TimerRunState.Running or TimerRunState.Paused ||
                                         CustomHours > 0 || CustomMinutes > 0 || CustomSeconds > 0;

    [RelayCommand(CanExecute = nameof(CanCancelTimer))]
    private void CancelTimer() => _service.CancelTimer();

    private bool CanCancelTimer() => Current.TimerState != TimerRunState.Idle;

    [RelayCommand(CanExecute = nameof(CanUseStopwatchPrimary))]
    private void StopwatchPrimary()
    {
        if (Current.IsStopwatchRunning)
        {
            _service.PauseStopwatch();
        }
        else
        {
            _service.StartStopwatch();
        }
    }

    private static bool CanUseStopwatchPrimary() => true;

    [RelayCommand(CanExecute = nameof(CanAddLap))]
    private void AddLap() => _service.AddLap();

    private bool CanAddLap() => Current.IsStopwatchRunning;

    [RelayCommand(CanExecute = nameof(CanResetStopwatch))]
    private void ResetStopwatch() => _service.ResetStopwatch();

    private bool CanResetStopwatch() => !Current.IsStopwatchRunning &&
                                        (Current.StopwatchElapsed > TimeSpan.Zero || Current.Laps.Count > 0);

    [RelayCommand(CanExecute = nameof(CanUseCompactPrimary))]
    private void CompactPrimary()
    {
        if (Current.TimerState == TimerRunState.Running)
        {
            _service.PauseTimer();
        }
        else if (Current.TimerState == TimerRunState.Paused)
        {
            _service.ResumeTimer();
        }
        else if (Current.IsStopwatchRunning)
        {
            _service.PauseStopwatch();
        }
        else if (HasStopwatchActivity)
        {
            SelectedToolIndex = 1;
            _service.StartStopwatch();
        }
    }

    private bool CanUseCompactPrimary() =>
        Current.TimerState is TimerRunState.Running or TimerRunState.Paused || HasStopwatchActivity;

    [RelayCommand(CanExecute = nameof(CanUseCompactSecondary))]
    private void CompactSecondary()
    {
        if (Current.TimerState != TimerRunState.Idle)
        {
            _service.CancelTimer();
        }
        else
        {
            _service.ResetStopwatch();
        }
    }

    private bool CanUseCompactSecondary() =>
        Current.TimerState != TimerRunState.Idle ||
        (!Current.IsStopwatchRunning && HasStopwatchActivity);

    partial void OnCustomHoursChanged(double value) => TimerPrimaryCommand.NotifyCanExecuteChanged();
    partial void OnCustomMinutesChanged(double value) => TimerPrimaryCommand.NotifyCanExecuteChanged();
    partial void OnCustomSecondsChanged(double value) => TimerPrimaryCommand.NotifyCanExecuteChanged();

    private void OnSnapshotChanged(object? sender, TimeToolsSnapshot snapshot)
    {
        lock (_snapshotSync)
        {
            _pendingSnapshot = snapshot;
            _pendingSnapshotVersion++;
        }

        QueueSnapshotDispatch();
    }

    private void QueueSnapshotDispatch()
    {
        if (_disposed ||
            Interlocked.CompareExchange(ref _snapshotDispatchPending, 1, 0) != 0)
        {
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            DrainLatestSnapshot();
            return;
        }

        if (!_dispatcher.TryEnqueue(DrainLatestSnapshot))
        {
            Volatile.Write(ref _snapshotDispatchPending, 0);
        }
    }

    private void DrainLatestSnapshot()
    {
        TimeToolsSnapshot snapshot;
        long version;
        lock (_snapshotSync)
        {
            snapshot = _pendingSnapshot;
            version = _pendingSnapshotVersion;
        }

        if (!_disposed)
        {
            Current = snapshot;
            OnPropertyChanged(nameof(LapTexts));
        }

        Volatile.Write(ref _snapshotDispatchPending, 0);
        lock (_snapshotSync)
        {
            if (_disposed || version == _pendingSnapshotVersion)
            {
                return;
            }
        }

        QueueSnapshotDispatch();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var value = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        return value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        OnPropertyChanged(nameof(TimerStatusText));
        OnPropertyChanged(nameof(TimerPrimaryText));
        OnPropertyChanged(nameof(TimerSecondaryText));
        OnPropertyChanged(nameof(StopwatchPrimaryText));
        OnPropertyChanged(nameof(CompactStatusText));
        OnPropertyChanged(nameof(CompactPrimaryText));
        OnPropertyChanged(nameof(CompactSecondaryText));
        OnPropertyChanged(nameof(LapTexts));
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _service.SnapshotChanged -= OnSnapshotChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public static ImmediateUiDispatcher Instance { get; } = new();
        public bool HasThreadAccess => true;
        public bool TryEnqueue(Action callback) { callback(); return true; }
    }
}
