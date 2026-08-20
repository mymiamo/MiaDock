namespace MiaDock.Platform.Windows.Tests.Interop;

[TestClass]
public sealed class NativeCallbackGuardTests
{
    // Window procedures and hook callbacks are invoked by Windows on a native
    // stack. A managed exception that unwinds out of one of them terminates the
    // process through a fail-fast that reports a bogus "stack buffer overrun"
    // and never reaches the logger, so each body has to open with a guard.
    private static readonly (string Path, string Signature)[] Callbacks =
    [
        (@"Overlay\OverlayWindowController.cs", "private nint WindowMessageHandler("),
        (@"Overlay\OverlayWindowController.cs", "private nint LowLevelMouseHandler("),
        (@"Windowing\WindowMinimumSizeMonitor.cs", "private nint WindowMessageHandler("),
        (@"Fullscreen\WindowsFullscreenDetectionService.cs", "private void OnWinEvent("),
        (@"Applications\WindowsApplicationActivityService.cs", "private void OnForegroundChanged("),
        (@"HotKeys\WindowsGlobalHotKeyService.cs", "private nint HandleWindowMessage("),
        (@"Lifecycle\WindowsSessionLockStateService.cs", "private nint HandleWindowMessage(")
    ];

    [TestMethod]
    public void NativeCallbacks_NeverLetManagedExceptionsUnwindIntoWindows()
    {
        foreach (var (path, signature) in Callbacks)
        {
            var source = File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "src",
                "MiaDock.Platform.Windows",
                path));

            var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.IsTrue(signatureIndex >= 0, $"{path} no longer declares {signature}.");

            var bodyIndex = source.IndexOf('{', signatureIndex);
            Assert.IsTrue(bodyIndex >= 0, $"{signature} has no body in {path}.");

            var body = source[(bodyIndex + 1)..].TrimStart();
            Assert.IsTrue(
                body.StartsWith("try", StringComparison.Ordinal),
                $"{signature} in {path} must wrap its work in a try/catch so the " +
                "process cannot fail fast when a callback throws.");
        }
    }

    [TestMethod]
    public void OverlayController_DoesNotTouchTheDispatcherQueueOfAClosedWindow()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MiaDock.Platform.Windows",
            "Overlay",
            "OverlayWindowController.cs"));

        StringAssert.Contains(source, "private bool EnqueueOnWindowThread(");
        Assert.DoesNotContain(
            "_window.DispatcherQueue.TryEnqueue",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MiaDock.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
