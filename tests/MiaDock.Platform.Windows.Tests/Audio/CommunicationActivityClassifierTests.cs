using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Platform.Windows.Audio;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class CommunicationActivityClassifierTests
{
    [TestMethod]
    [DataRow("discord.exe", "Discord", CommunicationApp.Discord)]
    [DataRow("SkypeApp.exe", "skypeapp", CommunicationApp.Skype)]
    [DataRow("WhatsApp.Native.exe", "WHATSAPPNATIVE", CommunicationApp.WhatsApp)]
    [DataRow("WhatsApp.Root.exe", "whatsapproot", CommunicationApp.WhatsApp)]
    [DataRow("DiscordSystemHelper.exe", "discordsystemhelper", CommunicationApp.Discord)]
    public void SupportedAppCaptureAndRender_ReturnsStrongDetectedResult(
        string capture,
        string render,
        CommunicationApp expectedApp)
    {
        var result = CommunicationActivityClassifier.Classify([Id(capture)], [Id(render)]);

        Assert.AreEqual(CallActivityState.Detected, result.State);
        Assert.AreEqual(expectedApp, result.App);
        Assert.IsTrue(result.HasCaptureActivity);
        Assert.IsTrue(result.HasRenderActivity);
    }

    [TestMethod]
    public void DiscordRenderOnly_DoesNotClaimCall()
    {
        var result = CommunicationActivityClassifier.Classify([], [Id("discord")]);

        Assert.AreEqual(CallActivityState.None, result.State);
    }

    [TestMethod]
    public void DiscordCaptureOnly_RemainsPossible()
    {
        var result = CommunicationActivityClassifier.Classify([Id("DiscordCanary.exe")], []);

        Assert.AreEqual(CallActivityState.Possible, result.State);
        Assert.AreEqual(CommunicationApp.Discord, result.App);
        Assert.IsFalse(result.HasRenderActivity);
    }

    [TestMethod]
    public void UnrelatedCaptureAndDiscordRender_DoesNotClaimDiscordCall()
    {
        var result = CommunicationActivityClassifier.Classify([Id("chrome")], [Id("discord")]);

        Assert.AreEqual(CallActivityState.None, result.State);
    }

    [TestMethod]
    public void DiscordCaptureAndUnrelatedRender_RemainsPossibleNotDetected()
    {
        var result = CommunicationActivityClassifier.Classify([Id("discord")], [Id("spotify")]);

        Assert.AreEqual(CallActivityState.Possible, result.State);
        Assert.AreEqual(CommunicationApp.Discord, result.App);
    }

    [TestMethod]
    public void UnsupportedCommunicationCapture_RemainsPossible()
    {
        var result = CommunicationActivityClassifier.Classify([Id("ms-teams")], [Id("ms-teams")]);

        Assert.AreEqual(CallActivityState.Possible, result.State);
        Assert.AreEqual(CommunicationApp.Teams, result.App);
    }

    [TestMethod]
    public void PreferredStrongAppWinsDeterministicallyWhileStillActive()
    {
        var result = CommunicationActivityClassifier.Classify(
            [Id("discord"), Id("whatsapp")],
            [Id("discord"), Id("whatsapp")],
            CommunicationApp.WhatsApp);

        Assert.AreEqual(CommunicationApp.WhatsApp, result.App);
    }

    private static CommunicationProcessIdentity Id(string name) => new(name, $"C:\\Apps\\{name}.exe");
}

[TestClass]
public sealed class CommunicationActivityStabilizerTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 8, 26, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void SingleShortEvidence_DoesNotBecomeDetected()
    {
        var stabilizer = new CommunicationActivityStabilizer();

        var result = stabilizer.Update(Strong(CommunicationApp.Discord), Epoch);
        result = stabilizer.Update(CommunicationActivityResult.None, Epoch.AddMilliseconds(400));

        Assert.AreNotEqual(CallActivityState.Detected, result.State);
    }

    [TestMethod]
    public void StableEvidence_BecomesDetectedAfterActivationDelay()
    {
        var stabilizer = new CommunicationActivityStabilizer();

        stabilizer.Update(Strong(CommunicationApp.Discord), Epoch);
        var result = stabilizer.Update(Strong(CommunicationApp.Discord), Epoch.AddMilliseconds(900));

        Assert.AreEqual(CallActivityState.Detected, result.State);
        Assert.AreEqual(Epoch.AddMilliseconds(900), result.StartedAt);
    }

    [TestMethod]
    public void ShortAudioGlitch_RemainsDetected()
    {
        var stabilizer = DetectDiscord();

        var result = stabilizer.Update(CommunicationActivityResult.None, Epoch.AddSeconds(1));
        result = stabilizer.Update(Strong(CommunicationApp.Discord), Epoch.AddSeconds(2));

        Assert.AreEqual(CallActivityState.Detected, result.State);
        Assert.AreEqual(CommunicationApp.Discord, result.App);
    }

    [TestMethod]
    public void SustainedEvidenceLoss_EndsDetectedCall()
    {
        var stabilizer = DetectDiscord();

        stabilizer.Update(CommunicationActivityResult.None, Epoch.AddSeconds(1));
        var result = stabilizer.Update(CommunicationActivityResult.None, Epoch.AddMilliseconds(2501));

        Assert.AreEqual(CallActivityState.None, result.State);
        Assert.IsNull(result.StartedAt);
    }

    [TestMethod]
    public void ProviderSwitch_DoesNotLeaveStaleProvider()
    {
        var stabilizer = DetectDiscord();

        stabilizer.Update(Strong(CommunicationApp.WhatsApp), Epoch.AddSeconds(1));
        var pending = stabilizer.Update(Strong(CommunicationApp.WhatsApp), Epoch.AddMilliseconds(2501));
        var detected = stabilizer.Update(Strong(CommunicationApp.WhatsApp), Epoch.AddMilliseconds(3401));

        Assert.AreEqual(CallActivityState.Possible, pending.State);
        Assert.AreEqual(CommunicationApp.WhatsApp, pending.App);
        Assert.AreEqual(CallActivityState.Detected, detected.State);
        Assert.AreEqual(CommunicationApp.WhatsApp, detected.App);
    }

    private static CommunicationActivityStabilizer DetectDiscord()
    {
        var stabilizer = new CommunicationActivityStabilizer();
        stabilizer.Update(Strong(CommunicationApp.Discord), Epoch);
        stabilizer.Update(Strong(CommunicationApp.Discord), Epoch.AddMilliseconds(900));
        return stabilizer;
    }

    private static CommunicationActivityResult Strong(CommunicationApp app)
    {
        var name = app.ToString();
        return new CommunicationActivityResult(
            CallActivityState.Detected,
            app,
            name,
            name,
            $"C:\\Apps\\{name}.exe",
            true,
            true);
    }
}
