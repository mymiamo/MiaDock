using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class LifecycleUiTests
{
    [TestMethod]
    public void CloseDialog_OffersMinimizeExitAndRememberOptions()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Dialogs",
            "CloseBehaviorDialog.xaml"));
        var attributes = document.Descendants().Attributes().Select(attribute => attribute.Value).ToArray();

        Assert.IsTrue(attributes.Contains("Sistem tepsisine küçült"));
        Assert.IsTrue(attributes.Contains("Uygulamadan tamamen çık"));
        Assert.IsTrue(attributes.Contains("Seçimimi hatırla"));
    }

    [TestMethod]
    public void StartupPage_ExplainsStoreCompatibleStartupTask()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "StartupShutdownSettingsPage.xaml"));
        var messages = document.Descendants().Attributes("Message").Select(attribute => attribute.Value);

        Assert.IsTrue(messages.Any(message => message.Contains("StartupTask API", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PackageManifest_DeclaresStartupTaskUsedByApplication()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Package.appxmanifest"));
        XNamespace desktop = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";

        var extension = document
            .Descendants(desktop + "Extension")
            .Single(element => (string?)element.Attribute("Category") == "windows.startupTask");
        var startupTask = extension.Element(desktop + "StartupTask");

        Assert.IsNotNull(startupTask);
        Assert.AreEqual("MiaDockStartupTask", (string?)startupTask.Attribute("TaskId"));
        Assert.AreEqual("false", (string?)startupTask.Attribute("Enabled"));
        Assert.AreEqual("MiaDock", (string?)startupTask.Attribute("DisplayName"));
        Assert.AreEqual("MiaDock.App.exe", (string?)extension.Attribute("Executable"));
        Assert.AreEqual("Windows.FullTrustApplication", (string?)extension.Attribute("EntryPoint"));
    }

    [TestMethod]
    public void ApplicationProject_PackagesTimerAlarmWaveFile()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "MiaDock.App.csproj"));
        var content = document
            .Descendants("Content")
            .Single(element =>
                (string?)element.Attribute("Include") == @"Assets\miadock-ringtone.wav");

        Assert.AreEqual(
            "PreserveNewest",
            (string?)content.Attribute("CopyToOutputDirectory"));
    }

    [TestMethod]
    public void StartupService_UsesRequestResultInsteadOfPotentiallyStaleTaskState()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Platform",
            "WindowsStartupTaskService.cs"));

        StringAssert.Contains(source, "state = await task.RequestEnableAsync()");
        StringAssert.Contains(source, "return Map(state)");
    }

    [TestMethod]
    public void FocusControls_PreserveAudioIndicatorAsRightmostCompactStatus()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "IdleCompactView.xaml"));
        var statusTray = document
            .Descendants()
            .Single(element => (string?)element.Attribute(
                XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "StatusTray");
        var namedChildren = statusTray
            .Elements()
            .Select(element => (string?)element.Attribute(
                XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")))
            .Where(name => name is not null)
            .ToArray();

        Assert.AreEqual("FocusStatus", namedChildren[^2]);
        Assert.AreEqual("MusicActivity", namedChildren[^1]);
    }

    [TestMethod]
    public void ExpandedHomeDock_KeepsFocusAndMusicInTwoColumns()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "IdleExpandedView.xaml"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var focus = document.Descendants()
            .Single(element => (string?)element.Attribute(xaml + "Name") == "FocusPanel");
        var music = document.Descendants()
            .Single(element => (string?)element.Attribute(xaml + "Name") == "MusicPanel");

        var focusCard = focus.Ancestors().First(element =>
            (string?)element.Attribute(xaml + "Name") == "FocusCard");
        Assert.IsNull((string?)focusCard.Attribute("Grid.Column"));
        Assert.AreEqual("1", (string?)music.Attribute("Grid.Column"));
    }

    [TestMethod]
    public void FocusQuickPanel_UsesExplicitDeactivateAndDurationActions()
    {
        var text = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "FocusQuickPanel.xaml"));

        StringAssert.Contains(text, "ActivateProfileCommand");
        StringAssert.Contains(text, "ItemsSource=\"{Binding QuickProfiles}\"");
        StringAssert.Contains(text, "MaximumRowsOrColumns=\"2\"");
        StringAssert.Contains(text, "IsEnabled=\"{Binding CanActivate}\"");
        StringAssert.Contains(text, "DeactivateCommand");
        StringAssert.Contains(text, "Click=\"OnDurationClick\"");
        StringAssert.Contains(text, "Tag=\"indefinite\"");
        StringAssert.Contains(text, "Opening=\"OnDurationFlyoutOpening\"");
        StringAssert.Contains(text, "Closed=\"OnDurationFlyoutClosed\"");
    }

    [TestMethod]
    public void Overlay_AppliesFocusToEventsVisibilityAndLifecycle()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "OverlayWindow.xaml.cs"));

        StringAssert.Contains(
            source,
            "_focusPolicy.Current.AllowsEvent(moduleEvent)");
        StringAssert.Contains(
            source,
            "focus.AllowsNormalDock(");
        StringAssert.Contains(
            source,
            "focus.AllowsTemporaryDock(_fullscreenState.IsFullscreen)");
        StringAssert.Contains(
            source,
            "_focusPolicy.PolicyChanged += OnFocusPolicyChanged");
        StringAssert.Contains(
            source,
            "_focusPolicy.PolicyChanged -= OnFocusPolicyChanged");
    }

    [TestMethod]
    public void Overlay_GuardsEveryModuleClickAndReportsViewLoadFailures()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "OverlayWindow.xaml.cs"));

        StringAssert.Contains(source, "TryRunDockAction");
        StringAssert.Contains(source, "OnModuleViewLoadFailed");
        StringAssert.Contains(source, "A dock interaction failed safely.");
        StringAssert.Contains(source, "SetInputActivationEnabled(isExpanded)");
        StringAssert.Contains(source, "SuspendTransientInteraction");
        StringAssert.Contains(source, "DockInteractionSession.IsActive");
    }

    [TestMethod]
    public void ExpandedHost_DoesNotAnimateRoutinePresentationTicks()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "ExpandedModuleHost.xaml.cs"));

        StringAssert.Contains(source, "_activeModuleId");
        StringAssert.Contains(source, "RequestContentMotionIfNeeded(contentChanged)");
        StringAssert.Contains(source, "if (!contentChanged && direction == MotionDirection.None)");
    }

    [TestMethod]
    public void WinUiExceptionBoundary_HandlesOnlyKnownRecoverableFailures()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Services",
            "AppExceptionCoordinator.cs"));

        StringAssert.Contains(source, "args.Handled = args.Exception is XamlParseException");
        StringAssert.Contains(source, "InvalidComObjectException");
        StringAssert.Contains(source, "ObjectDisposedException");
        Assert.DoesNotContain("args.Handled = true", source, StringComparison.Ordinal);
    }

    [TestMethod]
    public void TrayMenu_ProvidesGuaranteedFocusEscapeWhenDockCanBeHidden()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Services",
            "TrayMenuCoordinator.cs"));

        StringAssert.Contains(source, "DeactivateFocusCommand");
        StringAssert.Contains(source, "Tray.FocusTurnOff");
        StringAssert.Contains(source, "FocusAccessPolicy.RequiresTrayEscape(");
        StringAssert.Contains(source, "_focus.FocusChanged += OnFocusChanged");
        StringAssert.Contains(source, "_focus.FocusChanged -= OnFocusChanged");
        StringAssert.Contains(source, "_focus.Deactivate()");
        StringAssert.Contains(source, "_overlay.ShowDock()");
    }
}
