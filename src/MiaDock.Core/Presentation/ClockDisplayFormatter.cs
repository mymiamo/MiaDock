using System.Globalization;
using MiaDock.Core.Settings;

namespace MiaDock.Core.Presentation;

public readonly record struct ClockDisplayText(string Time, string Date);

public static class ClockDisplayFormatter
{
    public static ClockDisplayText Format(
        DateTimeOffset value,
        CultureInfo culture,
        ClockDisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(settings);

        var timePattern = settings.HourFormat == ClockHourFormat.TwelveHour
            ? settings.ShowSeconds ? "h:mm:ss tt" : "h:mm tt"
            : settings.ShowSeconds ? "HH:mm:ss" : "HH:mm";
        var datePattern = settings.DateFormat switch
        {
            ClockDateFormat.Long when settings.ShowWeekday => "dddd, d MMMM yyyy",
            ClockDateFormat.Long => "d MMMM yyyy",
            ClockDateFormat.Short when settings.ShowWeekday => "ddd, d MMM",
            _ => "d MMM"
        };

        return new ClockDisplayText(
            value.ToString(timePattern, culture),
            settings.ShowDate ? value.ToString(datePattern, culture) : string.Empty);
    }

    public static TimeSpan DelayUntilNextRefresh(
        DateTimeOffset value,
        ClockDisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var boundary = settings.ShowSeconds
            ? new DateTimeOffset(
                value.Year,
                value.Month,
                value.Day,
                value.Hour,
                value.Minute,
                value.Second,
                value.Offset).AddSeconds(1)
            : new DateTimeOffset(
                value.Year,
                value.Month,
                value.Day,
                value.Hour,
                value.Minute,
                0,
                value.Offset).AddMinutes(1);
        return TimeSpan.FromMilliseconds(Math.Max(10, (boundary - value).TotalMilliseconds));
    }
}
