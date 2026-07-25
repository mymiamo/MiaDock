using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.Core.Threading;
using MiaDock.Modules.Time.Models;
using MiaDock.Modules.Time.Services;

namespace MiaDock.Modules.Time.ViewModels;

public sealed partial class TimeToolsViewModel : ObservableObject, IDisposable
{
    private readonly ITimeToolsService _service;
    private readonly IUiDispatcher _dispatcher;
    private readonly object _snapshotSync = new();
    private TimeToolsSnapshot _pendingSnapshot;
    private long _pendingSnapshotVersion;
    private int _snapshotDispatchPending;
    private bool _disposed;

    public TimeToolsViewModel(ITimeToolsService service, IUiDispatcher? dispatcher = null)
    {
        _service = service;
        _dispatcher = dispatcher ?? ImmediateUiDispatcher.Instance;
        _current = service.Current;
        _pendingSnapshot = _current;
        _service.SnapshotChanged += OnSnapshotChanged;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimerText))]
    [NotifyPropertyChangedFor(nameof(TimerStatusText))]
    [NotifyPropertyChangedFor(nameof(TimerProgressPercent))]
    [NotifyPropertyChangedFor(nameof(TimerPrimaryText))]
    [NotifyPropertyChangedFor(nameof(TimerPrimaryGlyph))]
    [NotifyPropertyChangedFor(nameof(StopwatchText))]
    [NotifyPropertyChangedFor(nameof(StopwatchPrimaryText))]
    [NotifyPropertyChangedFor(nameof(CompactTimeText))]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(CompactPrimaryText))]
    [NotifyPropertyChangedFor(nameof(CompactPrimaryGlyph))]
    [NotifyPropertyChangedFor(nameof(CompactSecondaryText))]
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
        TimerRunState.Running => "Zamanlayıcı çalışıyor",
        TimerRunState.Paused => "Zamanlayıcı duraklatıldı",
        TimerRunState.Completed => "Süre doldu",
        _ => "Bir süre seçin"
    };

    public double TimerProgressPercent => Current.TimerProgress * 100;

    public string TimerPrimaryText => Current.TimerState switch
    {
        TimerRunState.Running => "Duraklat",
        TimerRunState.Paused => "Devam et",
        _ => "Başlat"
    };

    public string TimerPrimaryGlyph => Current.TimerState == TimerRunState.Running ? "\uE769" : "\uE768";

    public string StopwatchText => Current.StopwatchElapsed.ToString(@"hh\:mm\:ss\.f");

    public string StopwatchPrimaryText => Current.IsStopwatchRunning ? "Duraklat" : "Başlat";

    public string CompactTimeText => Current.TimerState is TimerRunState.Running or TimerRunState.Paused
        ? TimerText
        : HasStopwatchActivity
            ? Current.StopwatchElapsed.ToString(@"hh\:mm\:ss")
            : "00:00";

    public string CompactStatusText => Current.TimerState switch
    {
        TimerRunState.Running or TimerRunState.Paused => TimerStatusText,
        _ when Current.IsStopwatchRunning => "Kronometre çalışıyor",
        _ when HasStopwatchActivity => "Kronometre duraklatıldı",
        _ => "Zaman araçları"
    };

    public string CompactPrimaryText => Current.TimerState switch
    {
        TimerRunState.Running => "Duraklat",
        TimerRunState.Paused => "Devam et",
        _ when Current.IsStopwatchRunning => "Duraklat",
        _ when HasStopwatchActivity => "Devam et",
        _ => "Başlat"
    };

    public string CompactPrimaryGlyph =>
        Current.TimerState == TimerRunState.Running || Current.IsStopwatchRunning
            ? "\uE769"
            : "\uE768";

    public string CompactSecondaryText =>
        Current.TimerState != TimerRunState.Idle ? "Zamanlayıcıyı iptal et" : "Kronometreyi sıfırla";

    private bool HasStopwatchActivity =>
        Current.IsStopwatchRunning || Current.StopwatchElapsed > TimeSpan.Zero || Current.Laps.Count > 0;

    public IReadOnlyList<string> LapTexts => Current.Laps
        .Select((lap, index) => $"Tur {index + 1}  {lap:hh\\:mm\\:ss\\.f}")
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _service.SnapshotChanged -= OnSnapshotChanged;
    }

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public static ImmediateUiDispatcher Instance { get; } = new();
        public bool HasThreadAccess => true;
        public bool TryEnqueue(Action callback) { callback(); return true; }
    }
}
