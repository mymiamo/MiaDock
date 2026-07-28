using System.Globalization;
using MiaDock.Core.Presentation;
using MiaDock.Core.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class ClockDisplayFormatterTests
{
    private static readonly DateTimeOffset Sample =
        new(2026, 7, 28, 21, 5, 9, TimeSpan.FromHours(3));

    [TestMethod]
    public void Format_TwentyFourHourShortDate_UsesConfiguredParts()
    {
        var result = ClockDisplayFormatter.Format(
            Sample,
            CultureInfo.GetCultureInfo("tr-TR"),
            ClockDisplaySettings.Default);

        Assert.AreEqual("21:05", result.Time);
        StringAssert.Contains(result.Date, "28");
        StringAssert.Contains(result.Date, "Tem");
        StringAssert.Contains(result.Date, "Sal");
    }

    [TestMethod]
    public void Format_TwelveHourWithSecondsAndHiddenDate_HidesDate()
    {
        var settings = ClockDisplaySettings.Default with
        {
            HourFormat = ClockHourFormat.TwelveHour,
            ShowSeconds = true,
            ShowDate = false
        };

        var result = ClockDisplayFormatter.Format(
            Sample,
            CultureInfo.GetCultureInfo("en-US"),
            settings);

        Assert.AreEqual("9:05:09 PM", result.Time);
        Assert.AreEqual(string.Empty, result.Date);
    }

    [TestMethod]
    public void Format_LongDateWithoutWeekday_IncludesYearButNotWeekday()
    {
        var settings = ClockDisplaySettings.Default with
        {
            DateFormat = ClockDateFormat.Long,
            ShowWeekday = false
        };

        var result = ClockDisplayFormatter.Format(
            Sample,
            CultureInfo.GetCultureInfo("en-US"),
            settings);

        Assert.AreEqual("28 July 2026", result.Date);
        Assert.DoesNotContain("Tuesday", result.Date);
    }

    [TestMethod]
    public void DelayUntilNextRefresh_UsesSecondOrMinuteBoundary()
    {
        var withMilliseconds = Sample.AddMilliseconds(250);

        var secondDelay = ClockDisplayFormatter.DelayUntilNextRefresh(
            withMilliseconds,
            ClockDisplaySettings.Default with { ShowSeconds = true });
        var minuteDelay = ClockDisplayFormatter.DelayUntilNextRefresh(
            withMilliseconds,
            ClockDisplaySettings.Default);

        Assert.AreEqual(750, secondDelay.TotalMilliseconds, 0.1);
        Assert.AreEqual(50_750, minuteDelay.TotalMilliseconds, 0.1);
    }
}
