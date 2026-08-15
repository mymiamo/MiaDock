using MiaDock.Core.Modules;
using MiaDock.Platform.Windows.Audio;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class WindowsAudibleNotificationPlayerTests
{
    [TestMethod]
    public void Play_MapsCueToPackagedWaveAndLatestRequestWins()
    {
        var session = new RecordingSession();
        using var player = CreatePlayer(session, new ManualTimeProvider());

        player.Play(AudibleNotificationCue.DeviceConnected);
        player.Play(AudibleNotificationCue.DeviceDisconnected);
        player.Play(AudibleNotificationCue.Hourly);

        Assert.HasCount(3, session.Sources);
        Assert.IsTrue(session.Sources[0].AbsolutePath.EndsWith("/device-connected.wav", StringComparison.Ordinal));
        Assert.IsTrue(session.Sources[1].AbsolutePath.EndsWith("/device-left.wav", StringComparison.Ordinal));
        Assert.IsTrue(session.Sources[2].AbsolutePath.EndsWith("/per-hour-per.wav", StringComparison.Ordinal));
        Assert.AreEqual(session.Sources[2], session.CurrentSource);
        Assert.AreEqual(2, session.StopCount);
    }

    [TestMethod]
    public void Play_SuppressesSameCueForTwoSeconds()
    {
        var session = new RecordingSession();
        var time = new ManualTimeProvider();
        using var player = CreatePlayer(session, time);

        player.Play(AudibleNotificationCue.LowBattery);
        player.Play(AudibleNotificationCue.LowBattery);
        time.Advance(WindowsAudibleNotificationPlayer.DuplicateWindow);
        player.Play(AudibleNotificationCue.LowBattery);

        Assert.HasCount(2, session.Sources);
    }

    [TestMethod]
    public void Preview_BypassesDuplicateSuppression()
    {
        var session = new RecordingSession();
        using var player = CreatePlayer(session, new ManualTimeProvider());

        player.Preview(AudibleNotificationCue.NetworkOffline);
        player.Preview(AudibleNotificationCue.NetworkOffline);

        Assert.HasCount(2, session.Sources);
    }

    [TestMethod]
    public void MissingFile_FailsSilentlyWithoutCreatingPlaybackSession()
    {
        var sessionCreated = false;
        using var player = new WindowsAudibleNotificationPlayer(
            @"C:\MiaDock\Assets\sfx",
            () =>
            {
                sessionCreated = true;
                return new RecordingSession();
            },
            _ => false,
            new ManualTimeProvider());

        player.Play(AudibleNotificationCue.NetworkOffline);

        Assert.IsFalse(sessionCreated);
    }

    [TestMethod]
    public void Stop_StopsCurrentNotificationWithoutDisposingReusableSession()
    {
        var session = new RecordingSession();
        using var player = CreatePlayer(session, new ManualTimeProvider());

        player.Play(AudibleNotificationCue.DeviceConnected);
        player.Stop();

        Assert.AreEqual(1, session.StopCount);
        Assert.IsFalse(session.IsDisposed);
    }

    private static WindowsAudibleNotificationPlayer CreatePlayer(
        RecordingSession session,
        TimeProvider timeProvider) =>
        new(
            @"C:\MiaDock\Assets\sfx",
            () => session,
            _ => true,
            timeProvider);

    private sealed class RecordingSession : IAudibleNotificationPlaybackSession
    {
        public event EventHandler? Failed { add { } remove { } }
        public List<Uri> Sources { get; } = [];
        public Uri? CurrentSource { get; private set; }
        public int StopCount { get; private set; }
        public bool IsDisposed { get; private set; }

        public void Play(Uri source)
        {
            CurrentSource = source;
            Sources.Add(source);
        }

        public void Stop() => StopCount++;

        public void Dispose() => IsDisposed = true;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
