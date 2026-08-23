using MiaDock.Core.Settings;
using MiaDock.Core.Presentation;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class DockMetricsTests
{
    [TestMethod]
    public void CompactPreset_ProducesSafeSmallerMetrics()
    {
        var appearance = AppearanceSettings.Default with
        {
            Personalization = DockPersonalizationSettings.Default with
            {
                SizePreset = DockSizePreset.Compact
            }
        };

        var layout = DockMetrics.ToLayout(appearance);

        Assert.AreEqual(260, layout.CollapsedWidth);
        Assert.AreEqual(38, layout.CollapsedHeight);
        Assert.IsTrue(layout.ExpandedWidth < AppearanceSettings.Default.ExpandedWidth);
    }

    [TestMethod]
    public void CustomThickness_IsClampedBeforeMetricsAreResolved()
    {
        var tooSmall = AppearanceSettings.Default with
        {
            Personalization = DockPersonalizationSettings.Default with
            {
                SizePreset = DockSizePreset.Custom,
                CustomThickness = 5
            }
        };
        var tooLarge = tooSmall with
        {
            Personalization = tooSmall.EffectivePersonalization with { CustomThickness = 500 }
        };

        var small = DockMetrics.ToLayout(tooSmall);
        var large = DockMetrics.ToLayout(tooLarge);

        Assert.AreEqual(30, small.CollapsedHeight);
        Assert.AreEqual(54, large.CollapsedHeight);
    }

    [TestMethod]
    public void AppearancePreset_ChangesOnlySemanticPersonalization()
    {
        var customized = AppearanceSettings.Default with { AccentColor = "#123456" };

        var result = DockMetrics.ApplyPreset(customized, AppearancePreset.Large);

        Assert.AreEqual("#123456", result.AccentColor);
        Assert.AreEqual(AppearancePreset.Large, result.EffectivePersonalization.Preset);
        Assert.AreEqual(DockSizePreset.Large, result.EffectivePersonalization.SizePreset);
        Assert.AreEqual(DockDensity.Spacious, result.EffectivePersonalization.Density);
    }

    [TestMethod]
    public void SizePreset_PreservesIndividuallyConfiguredCornerRadii()
    {
        var radii = new DockCornerRadii(7, 13, 19, 25);
        var appearance = AppearanceSettings.Default with
        {
            CornerRadii = radii,
            LinkCornerRadii = false,
            Personalization = DockPersonalizationSettings.Default with
            {
                SizePreset = DockSizePreset.Compact
            }
        };

        var compact = DockMetrics.ToLayout(appearance);
        var large = DockMetrics.ToLayout(appearance with
        {
            Personalization = appearance.EffectivePersonalization with
            {
                SizePreset = DockSizePreset.Large
            }
        });

        Assert.AreEqual(radii, compact.EffectiveCornerRadii);
        Assert.AreEqual(radii, large.EffectiveCornerRadii);
        Assert.AreNotEqual(compact.CollapsedWidth, large.CollapsedWidth);
        Assert.AreNotEqual(compact.NotificationHeight, large.NotificationHeight);
    }

}
