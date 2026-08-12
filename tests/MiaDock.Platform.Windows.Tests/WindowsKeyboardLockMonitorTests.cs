using MiaDock.Core.Input;
using MiaDock.Platform.Windows.Input;

namespace MiaDock.Platform.Windows.Tests;

[TestClass]
public sealed class WindowsKeyboardLockMonitorTests
{
    [TestMethod]
    public async Task Start_EmitsOnlyStateChangesAfterBaseline()
    {
        var caps = (short)0;
        var num = (short)0;
        var scroll = (short)0;
        short GetState(int virtualKey) => virtualKey switch
        {
            0x14 => caps,
            0x90 => num,
            0x91 => scroll,
            _ => 0
        };

        await using var monitor = new WindowsKeyboardLockMonitor(GetState);
        var changes = new List<KeyboardLockStateChangedEventArgs>();
        monitor.StateChanged += (_, args) => changes.Add(args);

        await monitor.StartAsync();
        await Task.Delay(120);
        Assert.AreEqual(0, changes.Count);

        caps = 1;
        await Task.Delay(200);
        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(KeyboardLockKind.CapsLock, changes[0].Kind);
        Assert.IsTrue(changes[0].IsOn);

        num = 1;
        scroll = 1;
        await Task.Delay(200);
        Assert.AreEqual(3, changes.Count);
        Assert.IsTrue(changes.Any(change => change.Kind == KeyboardLockKind.NumLock && change.IsOn));
        Assert.IsTrue(changes.Any(change => change.Kind == KeyboardLockKind.ScrollLock && change.IsOn));

        await monitor.StopAsync();
    }
}
