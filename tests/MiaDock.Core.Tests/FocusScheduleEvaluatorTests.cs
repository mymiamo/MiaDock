using MiaDock.Core.Focus;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class FocusScheduleEvaluatorTests
{
    [TestMethod]
    public void SameDaySchedule_UsesStartInclusiveAndEndExclusive()
    {
        var schedule = new FocusSchedule(
            "work",
            true,
            FocusDays.Monday,
            9 * 60,
            17 * 60);

        Assert.IsTrue(FocusScheduleEvaluator.IsActive(
            schedule,
            Local(DayOfWeek.Monday, 9, 0)));
        Assert.IsTrue(FocusScheduleEvaluator.IsActive(
            schedule,
            Local(DayOfWeek.Monday, 16, 59)));
        Assert.IsFalse(FocusScheduleEvaluator.IsActive(
            schedule,
            Local(DayOfWeek.Monday, 17, 0)));
        Assert.IsFalse(FocusScheduleEvaluator.IsActive(
            schedule,
            Local(DayOfWeek.Tuesday, 10, 0)));
    }

    [TestMethod]
    public void OvernightSchedule_UsesPreviousDayAfterMidnight()
    {
        var schedule = new FocusSchedule(
            "sleep",
            true,
            FocusDays.Monday,
            22 * 60,
            7 * 60);

        Assert.IsTrue(FocusScheduleEvaluator.IsActive(
            schedule,
            Local(DayOfWeek.Monday, 23, 0)));
        Assert.IsTrue(FocusScheduleEvaluator.IsActive(
            schedule,
            Local(DayOfWeek.Tuesday, 6, 59)));
        Assert.IsFalse(FocusScheduleEvaluator.IsActive(
            schedule,
            Local(DayOfWeek.Tuesday, 7, 0)));
        Assert.IsFalse(FocusScheduleEvaluator.IsActive(
            schedule,
            Local(DayOfWeek.Monday, 6, 0)));
    }

    [TestMethod]
    public void EqualStartAndEnd_CoversSelectedWholeDay()
    {
        var schedule = new FocusSchedule(
            "all-day",
            true,
            FocusDays.Saturday,
            0,
            0);

        Assert.IsTrue(FocusScheduleEvaluator.IsActive(
            schedule,
            Local(DayOfWeek.Saturday, 13, 30)));
        Assert.IsFalse(FocusScheduleEvaluator.IsActive(
            schedule,
            Local(DayOfWeek.Sunday, 13, 30)));
    }

    [TestMethod]
    public void DisabledOrDaylessSchedule_IsNeverActive()
    {
        var now = Local(DayOfWeek.Monday, 10, 0);

        Assert.IsFalse(FocusScheduleEvaluator.IsActive(
            new FocusSchedule("disabled", false, FocusDays.Monday, 0, 0),
            now));
        Assert.IsFalse(FocusScheduleEvaluator.IsActive(
            new FocusSchedule("dayless", true, FocusDays.None, 0, 0),
            now));
    }

    [TestMethod]
    public void NextMinuteBoundary_RemovesSecondsAndAdvancesMinute()
    {
        var now = new DateTimeOffset(
            2026,
            7,
            29,
            12,
            34,
            45,
            TimeSpan.Zero);

        var next = FocusScheduleEvaluator.NextMinuteBoundary(
            now,
            TimeZoneInfo.Utc);

        Assert.AreEqual(
            new DateTimeOffset(2026, 7, 29, 12, 35, 0, TimeSpan.Zero),
            next);
    }

    private static DateTimeOffset Local(
        DayOfWeek day,
        int hour,
        int minute)
    {
        var date = new DateTime(2026, 7, 27);
        while (date.DayOfWeek != day)
        {
            date = date.AddDays(1);
        }

        return new DateTimeOffset(
            date.Year,
            date.Month,
            date.Day,
            hour,
            minute,
            0,
            TimeSpan.FromHours(3));
    }
}
