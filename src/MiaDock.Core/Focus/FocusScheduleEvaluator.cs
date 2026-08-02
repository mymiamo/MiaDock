namespace MiaDock.Core.Focus;

public static class FocusScheduleEvaluator
{
    public static bool IsActive(FocusSchedule schedule, DateTimeOffset localNow)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (!schedule.IsEnabled || schedule.Days == FocusDays.None)
        {
            return false;
        }

        var minute = (localNow.Hour * 60) + localNow.Minute;
        if (schedule.StartMinute == schedule.EndMinute)
        {
            return Includes(schedule.Days, localNow.DayOfWeek);
        }

        if (schedule.StartMinute < schedule.EndMinute)
        {
            return Includes(schedule.Days, localNow.DayOfWeek) &&
                   minute >= schedule.StartMinute &&
                   minute < schedule.EndMinute;
        }

        if (minute >= schedule.StartMinute)
        {
            return Includes(schedule.Days, localNow.DayOfWeek);
        }

        return minute < schedule.EndMinute &&
               Includes(schedule.Days, Previous(localNow.DayOfWeek));
    }

    public static DateTimeOffset NextMinuteBoundary(
        DateTimeOffset utcNow,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        var localNext = new DateTime(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            localNow.Hour,
            localNow.Minute,
            0,
            DateTimeKind.Unspecified).AddMinutes(1);
        while (timeZone.IsInvalidTime(localNext))
        {
            localNext = localNext.AddMinutes(1);
        }

        var nextUtc = TimeZoneInfo.ConvertTimeToUtc(localNext, timeZone);
        return new DateTimeOffset(nextUtc, TimeSpan.Zero);
    }

    public static FocusDays FromDayOfWeek(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => FocusDays.Monday,
        DayOfWeek.Tuesday => FocusDays.Tuesday,
        DayOfWeek.Wednesday => FocusDays.Wednesday,
        DayOfWeek.Thursday => FocusDays.Thursday,
        DayOfWeek.Friday => FocusDays.Friday,
        DayOfWeek.Saturday => FocusDays.Saturday,
        DayOfWeek.Sunday => FocusDays.Sunday,
        _ => FocusDays.None
    };

    private static bool Includes(FocusDays days, DayOfWeek day) =>
        (days & FromDayOfWeek(day)) != 0;

    private static DayOfWeek Previous(DayOfWeek day) =>
        day == DayOfWeek.Sunday ? DayOfWeek.Saturday : day - 1;
}
