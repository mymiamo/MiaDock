using MiaDock.Platform.Windows.Fullscreen;

namespace MiaDock.Platform.Windows.Tests.Fullscreen;

[TestClass]
public sealed class FullscreenClassifierTests
{
    [TestMethod]
    public void Classify_BorderlessWindowCoveringMonitor_IsFullscreen()
    {
        var result = FullscreenClassifier.Classify(CreateInput(
            new PixelBounds(-1920, 0, 0, 1080),
            new PixelBounds(-1920, 0, 0, 1080)));

        Assert.AreEqual(FullscreenDetectionReason.WindowCoversMonitor, result);
    }

    [TestMethod]
    public void Classify_MaximizedWindowUsingOnlyWorkArea_IsNotFullscreen()
    {
        var result = FullscreenClassifier.Classify(CreateInput(
            new PixelBounds(0, 0, 1920, 1040),
            new PixelBounds(0, 0, 1920, 1080)));

        Assert.AreEqual(FullscreenDetectionReason.None, result);
    }

    [TestMethod]
    public void Classify_StandardMaximizedBorderlessClient_IsNotFullscreen()
    {
        var full = new PixelBounds(0, 0, 1920, 1080);
        var input = CreateInput(full, full) with { IsStandardMaximizedWindow = true };

        Assert.AreEqual(FullscreenDetectionReason.None, FullscreenClassifier.Classify(input));
    }

    [TestMethod]
    public void Classify_ExclusiveDirect3DSignal_IsFullscreen()
    {
        var input = CreateInput(new PixelBounds(50, 50, 800, 600), new PixelBounds(0, 0, 1920, 1080)) with
        {
            UserNotificationState = 3
        };

        Assert.AreEqual(FullscreenDetectionReason.ExclusiveDirect3D, FullscreenClassifier.Classify(input));
    }

    [TestMethod]
    public void Classify_OwnOrCloakedWindows_AreIgnored()
    {
        var full = new PixelBounds(0, 0, 1920, 1080);
        Assert.AreEqual(FullscreenDetectionReason.None, FullscreenClassifier.Classify(CreateInput(full, full) with { IsOwnProcess = true }));
        Assert.AreEqual(FullscreenDetectionReason.None, FullscreenClassifier.Classify(CreateInput(full, full) with { IsCloaked = true }));
    }

    private static FullscreenEvaluationInput CreateInput(PixelBounds window, PixelBounds monitor) => new(
        true, false, false, false, false, false, window, monitor, 5);
}
