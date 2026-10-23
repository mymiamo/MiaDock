using MiaDock.Core.Advertising;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class AdSliderSessionTests
{
    [TestMethod]
    public void FiveMinuteRefreshDoesNotOverlapOrRepeatPerSlideAndCanResumeAfterAnOutage()
    {
        var schedule = new AdRefreshSchedule();
        Assert.IsTrue(schedule.TryBegin(TimeSpan.Zero));
        Assert.IsFalse(schedule.TryBegin(TimeSpan.FromSeconds(600)));
        schedule.Complete();
        Assert.IsFalse(schedule.TryBegin(TimeSpan.FromSeconds(299.999)));
        Assert.IsTrue(schedule.TryBegin(TimeSpan.FromSeconds(300)));
        schedule.Complete();
        for (var second = 301; second < 600; second++) Assert.IsFalse(schedule.TryBegin(TimeSpan.FromSeconds(second)));
        Assert.IsTrue(schedule.TryBegin(TimeSpan.FromSeconds(600)));
    }
    private static TimeSpan At(double seconds) => TimeSpan.FromSeconds(seconds);
    private static MiamoAd Ad(string name, string? creative = null) => new(name,
        new($"https://mymiamo.net/ads/{creative ?? name}.png"), new($"https://mymiamo.net/click/{name}"), $"public-{name}");
    private static AdSliderSession Ready(params MiamoAd[] ads)
    {
        var session = new AdSliderSession(); session.QueueAds(ads, At(0));
        if (session.Current is { } ad) session.MarkImageLoaded(ad.Key, At(0));
        session.SetVisible(true, At(0));
        return session;
    }

    [TestMethod]
    public void ManualFiveSecondMinimumStartsAfterImageLoadingAndAutomaticRotationLoops()
    {
        var session = new AdSliderSession(); session.QueueAds([Ad("a"), Ad("b")], At(0));
        session.SetVisible(true, At(0)); session.Tick(At(20));
        Assert.AreEqual("a", session.Current!.CampaignId);
        session.MarkImageLoaded(session.Current.Key, At(20));
        Assert.IsFalse(session.Move(1, At(24.99)));
        Assert.IsTrue(session.Move(1, At(25)));
        Assert.AreEqual("b", session.Current!.CampaignId);
        session.MarkImageLoaded(session.Current.Key, At(25)); session.SetVisible(true, At(25));
        session.Tick(At(30.99)); Assert.AreEqual("b", session.Current.CampaignId);
        session.Tick(At(31)); Assert.AreEqual("a", session.Current!.CampaignId);
    }

    [TestMethod]
    public void HiddenAndInactiveTimeDoesNotAdvanceOrConsumeRemainingExposure()
    {
        var session = Ready(Ad("a"), Ad("b"));
        session.SetVisible(false, At(2));
        session.Tick(At(500)); Assert.AreEqual(At(2), session.VisibleTime);
        Assert.IsFalse(session.Move(1, At(501)));
        session.SetVisible(true, At(502)); session.Tick(At(504.99));
        Assert.IsFalse(session.Move(1, At(504.99)));
        Assert.IsTrue(session.Move(1, At(505)));
        Assert.AreEqual("b", session.Current!.CampaignId);
    }

    [TestMethod]
    public void RefreshIncludingEmptyListPreservesCurrentMinimumAndDoesNotAdvanceWhileHidden()
    {
        var session = Ready(Ad("a"), Ad("b"));
        session.QueueAds([], At(1)); session.Tick(At(4.9));
        Assert.AreEqual("a", session.Current!.CampaignId);
        session.SetVisible(false, At(4.9)); session.Tick(At(100));
        Assert.IsNotNull(session.Current);
        session.SetVisible(true, At(101)); session.Tick(At(101.1));
        Assert.IsNull(session.Current); Assert.AreEqual(0, session.Count);
    }

    [TestMethod]
    public void ReplacementListWaitsAndThenUsesItsOrder()
    {
        var session = Ready(Ad("a"), Ad("b"));
        session.QueueAds([Ad("c"), Ad("d")], At(2)); session.Tick(At(4.999));
        Assert.AreEqual("a", session.Current!.CampaignId);
        session.Tick(At(5)); Assert.AreEqual("c", session.Current!.CampaignId);
        Assert.AreEqual("d", session.Next!.CampaignId);
    }

    [TestMethod]
    public void SingleAdvertisementStaysStaticAndHasNoSliderPositionControls()
    {
        var session = Ready(Ad("a")); var revision = session.Revision;
        session.Tick(At(1000));
        Assert.AreEqual(revision, session.Revision); Assert.IsNull(session.Next); Assert.IsFalse(session.CanMove);
        session.QueueAds([Ad("a") with { ImpressionToken = "new-public-token" }], At(1001)); session.Tick(At(1001));
        Assert.AreEqual(revision, session.Revision); Assert.IsTrue(session.ImageLoaded);
    }

    [TestMethod]
    public void BrokenImageIsSkippedAndAllBrokenImagesCollapseUntilNextSuccessfulRefresh()
    {
        var session = Ready(Ad("a"), Ad("b"), Ad("c"));
        session.MarkImageFailed(session.Current!.CreativeUrl, At(1));
        session.SkipFailed(At(1), canPresent: false); Assert.AreEqual("a", session.Current.CampaignId);
        session.SkipFailed(At(2), canPresent: true); Assert.AreEqual("b", session.Current!.CampaignId);
        session.MarkImageFailed(Ad("c").CreativeUrl, At(2)); Assert.IsNull(session.Next);
        session.MarkImageFailed(Ad("b").CreativeUrl, At(2)); session.SkipFailed(At(2), true);
        Assert.IsNull(session.Current); Assert.AreEqual(0, session.Count);
        session.QueueAds([Ad("c")], At(300)); Assert.AreEqual("c", session.Current!.CampaignId);
    }

    [TestMethod]
    public void KeyboardFocusCanPauseAutoplayWithoutPreventingManualNavigation()
    {
        var session = Ready(Ad("a"), Ad("b")); session.Tick(At(30), autoAdvance: false);
        Assert.AreEqual("a", session.Current!.CampaignId);
        Assert.IsTrue(session.Move(-1, At(30))); Assert.AreEqual("b", session.Current!.CampaignId);
    }

    [TestMethod]
    public void ImpressionRequiresRenderedVisibleTimeAndPreloadingDoesNotCount()
    {
        var session = Ready(Ad("a"), Ad("b")); var ledger = new AdImpressionLedger();
        Assert.IsNotNull(session.Next);
        Assert.IsNull(ledger.TryBegin(session, At(0)));
        session.Tick(At(.99)); Assert.IsNull(ledger.TryBegin(session, At(.99)));
        session.SetVisible(false, At(1)); Assert.IsNull(ledger.TryBegin(session, At(100)));
        session.SetVisible(true, At(101));
        Assert.AreEqual("a", ledger.TryBegin(session, At(101))!.CampaignId);
    }

    [TestMethod]
    public void RetriedImpressionRetainsEventIdAndOneCampaignIsCountedOncePerWindow()
    {
        var session = Ready(Ad("a"), Ad("b")); var ledger = new AdImpressionLedger();
        session.Tick(At(1)); var first = ledger.TryBegin(session, At(1))!;
        Assert.AreNotEqual(Guid.Empty, first.EventId); Assert.IsNull(ledger.TryBegin(session, At(1)));
        ledger.Complete(first, new(false, true), At(1));
        Assert.IsNull(ledger.TryBegin(session, At(2.99)));
        var retry = ledger.TryBegin(session, At(3))!; Assert.AreEqual(first, retry);
        ledger.Complete(retry, new(true, false), At(3));
        session.Move(1, At(5)); session.MarkImageLoaded(session.Current!.Key, At(5)); session.SetVisible(true, At(5));
        session.Move(1, At(10)); session.MarkImageLoaded(session.Current!.Key, At(10)); session.SetVisible(true, At(10)); session.Tick(At(11));
        Assert.AreEqual("a", session.Current!.CampaignId); Assert.IsNull(ledger.TryBegin(session, At(11)));
        var anotherWindow = new AdImpressionLedger(); Assert.AreNotEqual(first.EventId, anotherWindow.TryBegin(session, At(11))!.EventId);
    }

    [TestMethod]
    public void RetryBudgetIsBoundedAndPermanentFailuresAreNotReposted()
    {
        var session = Ready(Ad("a")); var ledger = new AdImpressionLedger(); session.Tick(At(1));
        var first = ledger.TryBegin(session, At(1))!; ledger.Complete(first, new(false, true), At(1));
        var second = ledger.TryBegin(session, At(3))!; ledger.Complete(second, new(false, true), At(3));
        var third = ledger.TryBegin(session, At(13))!; ledger.Complete(third, new(false, true), At(13));
        Assert.AreEqual(first.EventId, third.EventId); Assert.IsNull(ledger.TryBegin(session, At(100)));
        var permanent = new AdImpressionLedger(); var attempt = permanent.TryBegin(session, At(101))!;
        permanent.Complete(attempt, new(false, false), At(101)); Assert.IsNull(permanent.TryBegin(session, At(1000)));
    }

    [TestMethod]
    [DataRow(264d, 400d, 62d, 240d)]
    [DataRow(248d, 300d, 62d, 224d)]
    [DataRow(264d, 180d, 62d, 118d)]
    [DataRow(264d, 150d, 62d, 0d)]
    [DataRow(60d, 500d, 34d, 0d)]
    public void PanelUsesAvailableSpaceAndNeverEnlargesTheNavigationPane(double width, double height, double chrome, double expected) =>
        Assert.AreEqual(expected, AdPanelLayout.ImageSide(width, height, chrome));

    [TestMethod]
    [DataRow("https://mymiamo.net/ad.png", true)]
    [DataRow("https://www.mymiamo.net/ad.png", true)]
    [DataRow("http://mymiamo.net/ad.png", false)]
    [DataRow("https://mymiamo.net.evil.example/ad.png", false)]
    [DataRow("https://mymiamo.net@evil.example/ad.png", false)]
    [DataRow("https://evil.example@mymiamo.net/ad.png", false)]
    [DataRow("https://mymiamo.net:444/ad.png", false)]
    [DataRow("file:///C:/ad.png", false)]
    public void OnlyExplicitTrustedHttpsOriginsAreAccepted(string value, bool expected) =>
        Assert.AreEqual(expected, MiamoAdPolicy.IsTrusted(new(value)));
}
