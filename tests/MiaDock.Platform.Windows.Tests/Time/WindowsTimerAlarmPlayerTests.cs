using MiaDock.Platform.Windows.Time;

namespace MiaDock.Platform.Windows.Tests.Time;

[TestClass]
public sealed class WindowsTimerAlarmPlayerTests
{
    [TestMethod]
    public void Play_UsesPackagedWaveFileThroughMediaPlaybackSession()
    {
        var session = new RecordingSession();
        var fallbackCount = 0;
        using var player = new WindowsTimerAlarmPlayer(
            @"C:\MiaDock\Assets\miadock-ringtone.wav",
            () => session,
            path => path.EndsWith("miadock-ringtone.wav", StringComparison.Ordinal),
            () => fallbackCount++);

        player.Play();

        Assert.AreEqual(
            new Uri("file:///C:/MiaDock/Assets/miadock-ringtone.wav"),
            session.Source);
        Assert.AreEqual(0, fallbackCount);
    }

    [TestMethod]
    public void Play_IgnoresDuplicateRequestUntilCurrentAlarmCompletes()
    {
        var first = new RecordingSession();
        var second = new RecordingSession();
        var sessions = new Queue<RecordingSession>([first, second]);
        using var player = new WindowsTimerAlarmPlayer(
            @"C:\MiaDock\Assets\miadock-ringtone.wav",
            () => sessions.Dequeue(),
            _ => true,
            () => { });

        player.Play();
        player.Play();

        Assert.AreEqual(1, first.PlayCount);
        Assert.AreEqual(0, second.PlayCount);
        Assert.HasCount(1, sessions);
    }

    [TestMethod]
    public void Play_RepeatsExactlyFiveTimesThenAllowsAnotherAlarm()
    {
        var first = new RecordingSession();
        var second = new RecordingSession();
        var sessions = new Queue<RecordingSession>([first, second]);
        using var player = new WindowsTimerAlarmPlayer(
            @"C:\MiaDock\Assets\miadock-ringtone.wav",
            () => sessions.Dequeue(),
            _ => true,
            () => { });

        player.Play();
        for (var index = 0; index < WindowsTimerAlarmPlayer.AlarmPlayCount - 1; index++)
        {
            first.Complete();
        }

        Assert.AreEqual(WindowsTimerAlarmPlayer.AlarmPlayCount, first.PlayCount);
        Assert.IsFalse(first.IsDisposed);

        first.Complete();
        player.Play();

        Assert.AreEqual(1, second.PlayCount);
        Assert.IsTrue(first.IsDisposed);
    }

    [TestMethod]
    public void MediaEnded_CallbackDefersReplayUntilNativeCallbackHasReturned()
    {
        var session = new RecordingSession();
        var callbacks = new Queue<Action>();
        using var player = new WindowsTimerAlarmPlayer(
            @"C:\MiaDock\Assets\miadock-ringtone.wav",
            () => session,
            _ => true,
            () => { },
            callbackScheduler: callbacks.Enqueue);

        player.Play();
        session.Complete();

        Assert.AreEqual(1, session.PlayCount, "Replay must not run inside MediaEnded.");
        Assert.HasCount(1, callbacks);

        callbacks.Dequeue()();

        Assert.AreEqual(2, session.PlayCount);
        Assert.IsFalse(session.IsDisposed);
    }

    [TestMethod]
    public void DeferredMediaEnded_DoesNotReplayAfterStopDisposedTheSession()
    {
        var session = new RecordingSession();
        var callbacks = new Queue<Action>();
        using var player = new WindowsTimerAlarmPlayer(
            @"C:\MiaDock\Assets\miadock-ringtone.wav",
            () => session,
            _ => true,
            () => { },
            callbackScheduler: callbacks.Enqueue);

        player.Play();
        session.Complete();
        player.Stop();
        callbacks.Dequeue()();

        Assert.AreEqual(1, session.PlayCount);
        Assert.IsTrue(session.IsDisposed);
    }

    [TestMethod]
    public void Stop_ImmediatelyDisposesCurrentAlarmAndCancelsRemainingRepeats()
    {
        var first = new RecordingSession();
        var second = new RecordingSession();
        var sessions = new Queue<RecordingSession>([first, second]);
        using var player = new WindowsTimerAlarmPlayer(
            @"C:\MiaDock\Assets\miadock-ringtone.wav",
            () => sessions.Dequeue(),
            _ => true,
            () => { });

        player.Play();
        player.Stop();
        first.Complete();
        player.Play();

        Assert.IsTrue(first.IsDisposed);
        Assert.AreEqual(1, first.PlayCount);
        Assert.AreEqual(1, second.PlayCount);
    }

    [TestMethod]
    public void Play_FallsBackToSystemSoundWhenWaveFileIsMissing()
    {
        var fallbackCount = 0;
        using var player = new WindowsTimerAlarmPlayer(
            @"C:\missing.wav",
            () => new RecordingSession(),
            _ => false,
            () => fallbackCount++);

        player.Play();

        Assert.AreEqual(1, fallbackCount);
    }

    [TestMethod]
    public void Play_FallsBackWhenMediaPlaybackFails()
    {
        var session = new RecordingSession();
        var fallbackCount = 0;
        using var player = new WindowsTimerAlarmPlayer(
            @"C:\MiaDock\Assets\miadock-ringtone.wav",
            () => session,
            _ => true,
            () => fallbackCount++);

        player.Play();
        session.Fail();

        Assert.AreEqual(1, fallbackCount);
        Assert.IsTrue(session.IsDisposed);
    }

    private sealed class RecordingSession : IAlarmPlaybackSession
    {
        public event EventHandler? Completed;
        public event EventHandler? Failed;
        public Uri? Source { get; private set; }
        public bool IsDisposed { get; private set; }
        public int PlayCount { get; private set; }

        public void Play(Uri source)
        {
            Source = source;
            PlayCount++;
        }
        public void Replay() => PlayCount++;
        public void Complete() => Completed?.Invoke(this, EventArgs.Empty);
        public void Fail() => Failed?.Invoke(this, EventArgs.Empty);
        public void Dispose() => IsDisposed = true;
    }
}
