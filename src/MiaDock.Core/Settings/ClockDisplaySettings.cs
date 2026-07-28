namespace MiaDock.Core.Settings;

public enum ClockHourFormat
{
    TwentyFourHour,
    TwelveHour
}

public enum ClockDateFormat
{
    Short,
    Long
}

public sealed record ClockDisplaySettings(
    ClockHourFormat HourFormat,
    bool ShowSeconds,
    bool ShowDate,
    ClockDateFormat DateFormat,
    bool ShowWeekday)
{
    public static ClockDisplaySettings Default { get; } = new(
        ClockHourFormat.TwentyFourHour,
        ShowSeconds: false,
        ShowDate: true,
        ClockDateFormat.Short,
        ShowWeekday: true);
}
