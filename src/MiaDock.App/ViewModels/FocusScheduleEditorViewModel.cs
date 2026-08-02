using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Core.Focus;

namespace MiaDock.App.ViewModels;

public sealed class FocusScheduleEditorViewModel : ObservableObject
{
    private bool _isEnabled;
    private bool _monday;
    private bool _tuesday;
    private bool _wednesday;
    private bool _thursday;
    private bool _friday;
    private bool _saturday;
    private bool _sunday;
    private TimeSpan _startTime;
    private TimeSpan _endTime;

    public FocusScheduleEditorViewModel(FocusSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        Id = schedule.Id;
        _isEnabled = schedule.IsEnabled;
        _monday = Has(schedule.Days, FocusDays.Monday);
        _tuesday = Has(schedule.Days, FocusDays.Tuesday);
        _wednesday = Has(schedule.Days, FocusDays.Wednesday);
        _thursday = Has(schedule.Days, FocusDays.Thursday);
        _friday = Has(schedule.Days, FocusDays.Friday);
        _saturday = Has(schedule.Days, FocusDays.Saturday);
        _sunday = Has(schedule.Days, FocusDays.Sunday);
        _startTime = TimeSpan.FromMinutes(schedule.StartMinute);
        _endTime = TimeSpan.FromMinutes(schedule.EndMinute);
    }

    public string Id { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool Monday
    {
        get => _monday;
        set => SetProperty(ref _monday, value);
    }

    public bool Tuesday
    {
        get => _tuesday;
        set => SetProperty(ref _tuesday, value);
    }

    public bool Wednesday
    {
        get => _wednesday;
        set => SetProperty(ref _wednesday, value);
    }

    public bool Thursday
    {
        get => _thursday;
        set => SetProperty(ref _thursday, value);
    }

    public bool Friday
    {
        get => _friday;
        set => SetProperty(ref _friday, value);
    }

    public bool Saturday
    {
        get => _saturday;
        set => SetProperty(ref _saturday, value);
    }

    public bool Sunday
    {
        get => _sunday;
        set => SetProperty(ref _sunday, value);
    }

    public TimeSpan StartTime
    {
        get => _startTime;
        set => SetProperty(ref _startTime, ClampTime(value));
    }

    public TimeSpan EndTime
    {
        get => _endTime;
        set => SetProperty(ref _endTime, ClampTime(value));
    }

    public bool TryBuild(out FocusSchedule schedule)
    {
        var days =
            (Monday ? FocusDays.Monday : FocusDays.None) |
            (Tuesday ? FocusDays.Tuesday : FocusDays.None) |
            (Wednesday ? FocusDays.Wednesday : FocusDays.None) |
            (Thursday ? FocusDays.Thursday : FocusDays.None) |
            (Friday ? FocusDays.Friday : FocusDays.None) |
            (Saturday ? FocusDays.Saturday : FocusDays.None) |
            (Sunday ? FocusDays.Sunday : FocusDays.None);
        schedule = new FocusSchedule(
            Id,
            IsEnabled,
            days,
            ToMinute(StartTime),
            ToMinute(EndTime));
        return days != FocusDays.None;
    }

    private static bool Has(FocusDays value, FocusDays day) =>
        (value & day) != 0;

    private static int ToMinute(TimeSpan value) =>
        Math.Clamp((int)value.TotalMinutes, 0, 1439);

    private static TimeSpan ClampTime(TimeSpan value) =>
        TimeSpan.FromMinutes(ToMinute(value));
}
