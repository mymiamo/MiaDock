using MiaDock.App.Services;
using MiaDock.Core.Focus;
using MiaDock.Core.Modules;
using MiaDock.Core.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class PresentationPrivacyPolicyTests
{
    private static readonly ModulePresentation SensitivePresentation = new(
        "notifications",
        "private title",
        string.Empty,
        string.Empty,
        ModuleIndicatorKind.None,
        isSensitive: true);

    [TestMethod]
    public void FocusCannotLoosenGlobalSensitiveContentRestriction()
    {
        var focus = new FixedFocusPolicyService(Policy(
            fullscreen: true,
            locked: true));
        var policy = new PresentationPrivacyPolicy(focus);

        Assert.IsFalse(policy.CanPresent(
            SensitivePresentation,
            MiaDockSettings.Default,
            isFullscreen: true,
            isSessionLocked: false));
        Assert.IsFalse(policy.CanPresent(
            SensitivePresentation,
            MiaDockSettings.Default,
            isFullscreen: false,
            isSessionLocked: true));
    }

    [TestMethod]
    public void FocusCanTightenEnabledGlobalSensitiveContentPermission()
    {
        var settings = MiaDockSettings.Default with
        {
            Privacy = MiaDockSettings.Default.Privacy with
            {
                ShowSensitiveContentInFullscreen = true,
                ShowSensitiveContentWhenLocked = true
            }
        };
        var focus = new FixedFocusPolicyService(Policy(
            fullscreen: false,
            locked: false));
        var policy = new PresentationPrivacyPolicy(focus);

        Assert.IsFalse(policy.CanPresent(
            SensitivePresentation,
            settings,
            isFullscreen: true,
            isSessionLocked: false));
        Assert.IsFalse(policy.CanPresent(
            SensitivePresentation,
            settings,
            isFullscreen: false,
            isSessionLocked: true));
        Assert.IsTrue(policy.CanPresent(
            SensitivePresentation,
            settings,
            isFullscreen: false,
            isSessionLocked: false));
    }

    [TestMethod]
    public void NonSensitivePresentationIsAlwaysAllowed()
    {
        var focus = new FixedFocusPolicyService(Policy(
            fullscreen: false,
            locked: false));
        var policy = new PresentationPrivacyPolicy(focus);
        var presentation = SensitivePresentation with { };
        presentation = new ModulePresentation(
            presentation.ModuleId,
            presentation.PrimaryText,
            presentation.SecondaryText,
            presentation.LeadingGlyph,
            presentation.Indicator,
            isSensitive: false);

        Assert.IsTrue(policy.CanPresent(
            presentation,
            MiaDockSettings.Default,
            isFullscreen: true,
            isSessionLocked: true));
    }

    private static FocusPolicySnapshot Policy(bool fullscreen, bool locked) =>
        new(
            true,
            "privacy",
            FocusDockVisibility.EventsOnly,
            new HashSet<string>(StringComparer.Ordinal),
            ModuleEventPriority.Low,
            true,
            fullscreen,
            locked);

    private sealed class FixedFocusPolicyService(FocusPolicySnapshot current)
        : IFocusPolicyService
    {
        public FocusPolicySnapshot Current { get; } = current;

        public event EventHandler? PolicyChanged
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
        }
    }
}
