using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.Core.Localization;
using MiaDock.Core.Threading;
using MiaDock.Modules.Time.Models;
using MiaDock.Modules.Time.Services;

namespace MiaDock.Modules.Time.ViewModels;

public sealed partial class TimerPresetOption(int minutes, string label) : ObservableObject
{
    public int Minutes { get; } = minutes;

    public string Label { get; } = label;

    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class TimeToolsViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan MaximumTimerDuration = TimeSpan.FromHours(99);
    private static readonly int[] PresetMinuteValues = [5, 10, 15, 25, 45];
    private readonly ITimeToolsService _service;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILocalizationService? _localization;
    private readonly object _snapshotSync = new();
    private TimeToolsSnapshot _pendingSnapshot;
    private long _pendingSnapshotVersion;
    private int _snapshotDispatchPending;
    private bool _applyingPreset;
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
        PresetDurations = CreatePresetDurations();
        _service.SnapshotChanged += OnSnapshotChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
    }

    private TimeToolsSnapshot _current;
    private int _selectedToolIndex;

    public TimeToolsSnapshot Current => _current;

    private double _customHours;
    private double _customMinutes = 5;
    private double _customSeconds;

    public double CustomHours
    {
        get => _customHours;
        set => SetDurationPart(ref _customHours, value, 99, nameof(CustomHours));
    }

    public double CustomMinutes
    {
        get => _customMinutes;
        set => SetDurationPart(ref _customMinutes, value, 59, nameof(CustomMinutes));
    }

    public double CustomSeconds
    {
        get => _customSeconds;
        set => SetDurationPart(ref _customSeconds, value, 59, nameof(CustomSeconds));
    }

    public int SelectedToolIndex
    {
        get => _selectedToolIndex;
        set => SetProperty(ref _selectedToolIndex, value == 1 ? 1 : 0);
    }

    public IReadOnlyList<TimerPresetOption> PresetDurations { get; private set; }

    public string TimerText => FormatDuration(
        Current.TimerState == TimerRunState.Idle ? BuildCustomDuration() : Current.TimerRemaining);

    public string TimerStatusText => Current.TimerState switch
    {
        TimerRunState.Running => Text("Timer.Running", "Zamanlayıcı çalışıyor"),
        TimerRunState.Paused => Text("Timer.Paused", "Zamanlayıcı duraklatıldı"),
        TimerRunState.Completed => Text("Timer.Completed", "Süre doldu"),
        _ => Text("Timer.SelectDuration", "Bir süre seçin")
    };

    public double TimerProgressPercent => Current.TimerProgress * 100;

    public string TimerProgressAccessibleText =>
        $"{TimerStatusText}, {TimerProgressPercent.ToString("0", _localization?.CurrentCulture)}%";

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

    public string StopwatchPrimaryGlyph => Current.IsStopwatchRunning ? "\uE769" : "\uE768";

    public string StopwatchStatusText => Current.IsStopwatchRunning
        ? Text("Timer.StopwatchRunning", "Kronometre çalışıyor")
        : HasStopwatchActivity
            ? Text("Timer.StopwatchPaused", "Kronometre duraklatıldı")
            : Text("Timer.Tools", "Zaman araçları");

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
    public bool IsTimerIdle => Current.TimerState == TimerRunState.Idle;
    public bool HasTimerActivity => !IsTimerIdle;
    public bool IsTimerSetupEnabled => IsTimerIdle;

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

    public bool HasStopwatchActivity =>
        Current.IsStopwatchRunning || Current.StopwatchElapsed > TimeSpan.Zero || Current.Laps.Count > 0;

    public IReadOnlyList<string> LapTexts => Current.Laps
        .Select((lap, index) => Text("Timer.Lap", "Tur {0}  {1}", index + 1, lap.ToString(@"hh\:mm\:ss\.f")))
        .Reverse()
        .ToArray();

    [RelayCommand(CanExecute = nameof(CanSelectPreset))]
    private void SelectPreset(TimerPresetOption? preset)
    {
        if (preset is not { Minutes: > 0 } || preset.Minutes > 99 * 60)
        {
            return;
        }

        SelectedToolIndex = 0;
        _applyingPreset = true;
        try
        {
            CustomHours = preset.Minutes / 60;
            CustomMinutes = preset.Minutes % 60;
            CustomSeconds = 0;
        }
        finally
        {
            _applyingPreset = false;
        }

        foreach (var option in PresetDurations)
        {
            option.IsSelected = option.Minutes == preset.Minutes;
        }
    }

    private bool CanSelectPreset(TimerPresetOption? preset) =>
        IsTimerSetupEnabled && preset is { Minutes: > 0 } &&
        preset.Minutes <= MaximumTimerDuration.TotalMinutes;

    [RelayCommand(CanExecute = nameof(CanUseTimerPrimary))]
    private void TimerPrimary()
    {
        SelectedToolIndex = 0;
        switch (Current.TimerState)
        {
            case TimerRunState.Running:
                _service.PauseTimer();
                break;
            case TimerRunState.Paused:
                _service.ResumeTimer();
                break;
            default:
                _service.StartTimer(BuildCustomDuration());
                break;
        }
    }

    private bool CanUseTimerPrimary() => Current.TimerState is TimerRunState.Running or TimerRunState.Paused ||
                                          BuildCustomDuration() > TimeSpan.Zero;

    [RelayCommand(CanExecute = nameof(CanCancelTimer))]
    private void CancelTimer() => _service.CancelTimer();

    private bool CanCancelTimer() => Current.TimerState != TimerRunState.Idle;

    [RelayCommand(CanExecute = nameof(CanUseStopwatchPrimary))]
    private void StopwatchPrimary()
    {
        SelectedToolIndex = 1;
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
            ApplySnapshot(snapshot);
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

    private void ApplySnapshot(TimeToolsSnapshot snapshot)
    {
        var previous = _current;
        if (previous == snapshot)
        {
            return;
        }

        var previousHasStopwatchActivity = HasStopwatchActivityFor(previous);
        var currentHasStopwatchActivity = HasStopwatchActivityFor(snapshot);
        var timerStateChanged = previous.TimerState != snapshot.TimerState;
        var stopwatchStateChanged = previous.IsStopwatchRunning != snapshot.IsStopwatchRunning;
        var lapsChanged = !previous.Laps.SequenceEqual(snapshot.Laps);
        var timerTextChanged = FormatDuration(previous.TimerRemaining) != FormatDuration(snapshot.TimerRemaining);
        var stopwatchTextChanged = FormatStopwatch(previous.StopwatchElapsed) != FormatStopwatch(snapshot.StopwatchElapsed);
        var compactTimeChanged = FormatCompactTime(previous) != FormatCompactTime(snapshot);

        _current = snapshot;
        OnPropertyChanged(nameof(Current));

        if (timerTextChanged)
        {
            OnPropertyChanged(nameof(TimerText));
        }

        if (previous.TimerRemaining != snapshot.TimerRemaining ||
            previous.TimerDuration != snapshot.TimerDuration)
        {
            OnPropertyChanged(nameof(TimerProgressPercent));
            OnPropertyChanged(nameof(TimerProgressAccessibleText));
        }

        if (timerStateChanged)
        {
            OnPropertyChanged(nameof(TimerStatusText));
            OnPropertyChanged(nameof(TimerPrimaryText));
            OnPropertyChanged(nameof(TimerSecondaryText));
            OnPropertyChanged(nameof(TimerPrimaryGlyph));
            OnPropertyChanged(nameof(IsTimerCompleted));
            OnPropertyChanged(nameof(IsTimerIdle));
            OnPropertyChanged(nameof(HasTimerActivity));
            OnPropertyChanged(nameof(IsTimerSetupEnabled));
            OnPropertyChanged(nameof(TimerProgressAccessibleText));
            TimerPrimaryCommand.NotifyCanExecuteChanged();
            CancelTimerCommand.NotifyCanExecuteChanged();
            SelectPresetCommand.NotifyCanExecuteChanged();
        }

        if (stopwatchTextChanged)
        {
            OnPropertyChanged(nameof(StopwatchText));
        }

        if (stopwatchStateChanged)
        {
            OnPropertyChanged(nameof(StopwatchPrimaryText));
            OnPropertyChanged(nameof(StopwatchPrimaryGlyph));
            OnPropertyChanged(nameof(StopwatchStatusText));
            AddLapCommand.NotifyCanExecuteChanged();
        }

        if (previousHasStopwatchActivity != currentHasStopwatchActivity)
        {
            OnPropertyChanged(nameof(HasStopwatchActivity));
            OnPropertyChanged(nameof(StopwatchStatusText));
        }

        if (lapsChanged)
        {
            OnPropertyChanged(nameof(LapTexts));
        }

        if (compactTimeChanged)
        {
            OnPropertyChanged(nameof(CompactTimeText));
        }

        if (timerStateChanged || stopwatchStateChanged ||
            previousHasStopwatchActivity != currentHasStopwatchActivity)
        {
            OnPropertyChanged(nameof(CompactStatusText));
            OnPropertyChanged(nameof(CompactPrimaryText));
            OnPropertyChanged(nameof(CompactPrimaryGlyph));
            OnPropertyChanged(nameof(CompactSecondaryText));
        }

        if (stopwatchStateChanged || lapsChanged ||
            previous.StopwatchElapsed != snapshot.StopwatchElapsed)
        {
            ResetStopwatchCommand.NotifyCanExecuteChanged();
        }

        if (timerStateChanged || stopwatchStateChanged ||
            previousHasStopwatchActivity != currentHasStopwatchActivity)
        {
            CompactPrimaryCommand.NotifyCanExecuteChanged();
            CompactSecondaryCommand.NotifyCanExecuteChanged();
        }
    }

    private TimeSpan BuildCustomDuration()
    {
        var duration = TimeSpan.FromHours(NormalizeDurationPart(CustomHours, 99)) +
                       TimeSpan.FromMinutes(NormalizeDurationPart(CustomMinutes, 59)) +
                       TimeSpan.FromSeconds(NormalizeDurationPart(CustomSeconds, 59));
        return duration > MaximumTimerDuration ? MaximumTimerDuration : duration;
    }

    private static double NormalizeDurationPart(double value, double maximum) =>
        double.IsFinite(value) ? Math.Clamp(Math.Truncate(value), 0, maximum) : 0;

    private void SetDurationPart(ref double field, double value, double maximum, string propertyName)
    {
        var normalized = NormalizeDurationPart(value, maximum);
        if (!SetProperty(ref field, normalized, propertyName))
        {
            return;
        }

        if (!_applyingPreset)
        {
            foreach (var preset in PresetDurations)
            {
                preset.IsSelected = false;
            }
        }

        OnPropertyChanged(nameof(TimerText));
        TimerPrimaryCommand.NotifyCanExecuteChanged();
    }

    private static bool HasStopwatchActivityFor(TimeToolsSnapshot snapshot) =>
        snapshot.IsStopwatchRunning || snapshot.StopwatchElapsed > TimeSpan.Zero || snapshot.Laps.Count > 0;

    private static string FormatStopwatch(TimeSpan elapsed) => elapsed.ToString(@"hh\:mm\:ss\.f");

    private static string FormatCompactTime(TimeToolsSnapshot snapshot) =>
        snapshot.TimerState is TimerRunState.Running or TimerRunState.Paused
            ? FormatDuration(snapshot.TimerRemaining)
            : HasStopwatchActivityFor(snapshot)
                ? snapshot.StopwatchElapsed.ToString(@"hh\:mm\:ss")
                : "00:00";

    private static string FormatDuration(TimeSpan duration)
    {
        var value = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        return value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        var selectedPresetMinutes = PresetDurations.FirstOrDefault(option => option.IsSelected)?.Minutes;
        PresetDurations = CreatePresetDurations();
        if (selectedPresetMinutes is not null)
        {
            foreach (var preset in PresetDurations)
            {
                preset.IsSelected = preset.Minutes == selectedPresetMinutes;
            }
        }

        OnPropertyChanged(nameof(PresetDurations));
        OnPropertyChanged(nameof(TimerStatusText));
        OnPropertyChanged(nameof(TimerProgressAccessibleText));
        OnPropertyChanged(nameof(TimerPrimaryText));
        OnPropertyChanged(nameof(TimerSecondaryText));
        OnPropertyChanged(nameof(StopwatchPrimaryText));
        OnPropertyChanged(nameof(StopwatchStatusText));
        OnPropertyChanged(nameof(CompactStatusText));
        OnPropertyChanged(nameof(CompactPrimaryText));
        OnPropertyChanged(nameof(CompactSecondaryText));
        OnPropertyChanged(nameof(LapTexts));
    }

    private IReadOnlyList<TimerPresetOption> CreatePresetDurations()
    {
        var unit = Text("Timer.MinuteShort", "dk");
        return PresetMinuteValues
            .Select(minutes => new TimerPresetOption(minutes, $"{minutes} {unit}"))
            .ToArray();
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
