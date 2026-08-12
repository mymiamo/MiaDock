using MiaDock.Core.Presentation;
using MiaDock.Core.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class FullscreenDockVisibilityPolicyTests
{
    [TestMethod]
    [DataRow(FullscreenDockBehavior.HideCompletely, false, false)]
    [DataRow(FullscreenDockBehavior.NotificationsOnly, false, false)]
    [DataRow(FullscreenDockBehavior.EdgeReveal, true, true)]
    [DataRow(FullscreenDockBehavior.KeepVisible, true, false)]
    public void Evaluate_AppliesAllFourModes(
        FullscreenDockBehavior behavior,
        bool expectedShow,
        bool expectedEdgeHidden)
    {
        var result = FullscreenDockVisibilityPolicy.Evaluate(Context(behavior));

        Assert.AreEqual(expectedShow, result.ShowWindow);
        Assert.AreEqual(expectedEdgeHidden, result.HideAtEdge);
        Assert.IsTrue(result.FullscreenPolicyApplied);
    }

    [TestMethod]
    public void Evaluate_DifferentDisplayLeavesNormalVisibilityUntouched()
    {
        var result = FullscreenDockVisibilityPolicy.Evaluate(
            Context(FullscreenDockBehavior.HideCompletely) with
            {
                FullscreenAffectsDockDisplay = false
            });

        Assert.IsTrue(result.ShowWindow);
        Assert.IsFalse(result.FullscreenPolicyApplied);
    }

    [TestMethod]
    public void Evaluate_NotificationAndHoverAreIndependentReasons()
    {
        var hover = FullscreenDockVisibilityPolicy.Evaluate(
            Context(FullscreenDockBehavior.EdgeReveal) with { HoverRevealActive = true });
        var notification = FullscreenDockVisibilityPolicy.Evaluate(
            Context(FullscreenDockBehavior.EdgeReveal) with
            {
                NotificationVisible = true,
                NotificationAllowed = true
            });

        Assert.IsFalse(hover.HideAtEdge);
        Assert.IsFalse(notification.HideAtEdge);
    }

    [TestMethod]
    public void Evaluate_InteractionOrExpandedPreventsEdgeHide()
    {
        var interaction = FullscreenDockVisibilityPolicy.Evaluate(
            Context(FullscreenDockBehavior.EdgeReveal) with { InteractionActive = true });
        var expanded = FullscreenDockVisibilityPolicy.Evaluate(
            Context(FullscreenDockBehavior.EdgeReveal) with { Expanded = true });

        Assert.IsFalse(interaction.HideAtEdge);
        Assert.IsFalse(expanded.HideAtEdge);
    }

    private static FullscreenDockVisibilityContext Context(FullscreenDockBehavior behavior) =>
        new(
            behavior,
            FullscreenAffectsDockDisplay: true,
            ManuallyHidden: false,
            NormalVisibilityAllowed: true,
            NotificationVisible: false,
            NotificationAllowed: false,
            HoverRevealActive: false,
            InteractionActive: false,
            PointerPressed: false,
            Expanded: false);
}
