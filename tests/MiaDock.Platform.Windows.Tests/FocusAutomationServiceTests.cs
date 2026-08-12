using MiaDock.App.Services;
using MiaDock.Core.Focus;
using MiaDock.Core.Settings;
using MiaDock.Core.Threading;
using MiaDock.Modules.Time.Services;
using MiaDock.Platform.Windows.Fullscreen;

namespace MiaDock.Platform.Windows.Tests;

[TestClass]
public sealed class FocusAutomationServiceTests
{
    private static readonly DateTimeOffset TestNow =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void ForegroundRule_WinsOverRunningAndSchedule()
    {
        var foreground = Profile(
            "foreground",
            rules:
            [
                new FocusActivationRule(
                    "fg",
                    true,
                    FocusActivationRuleKind.ApplicationForeground,
                    "code.exe")
            ]);
        var running = Profile(
            "running",
            rules:
            [
                new FocusActivationRule(
                    "run",
                    true,
                    FocusActivationRuleKind.ApplicationRunning,
                    "code.exe")
            ]);
        var scheduled = Profile(
            "scheduled",
            schedules:
            [
                new FocusSchedule(
                    "always",
                    true,
                    FocusDays.EveryDay,
                    0,
                    0)
            ]);
        var context = CreateContext([foreground, running, scheduled]);
        context.Applications.Set(
            "code.exe",
            ["code.exe"],
            processMonitoringAvailable: true);
        using var service = context.CreateService();

        service.Start();

        Assert.AreEqual("foreground", context.Focus.Current.ActiveProfile?.Id);
        Assert.AreEqual(
            FocusActivationSource.Automation,
            context.Focus.Current.ActiveState?.Source);
    }

    [TestMethod]
    public void ManualFocus_IsNotOverwrittenByAutomation()
    {
        var profile = Profile(
            "automatic",
            rules:
            [
                new FocusActivationRule(
                    "fg",
                    true,
                    FocusActivationRuleKind.ApplicationForeground,
                    "game.exe")
            ]);
        var context = CreateContext([profile]);
        context.Applications.Set(
            "game.exe",
            ["game.exe"],
            processMonitoringAvailable: true);
        context.Focus.SetManual(profile.Id);
        using var service = context.CreateService();

        service.Start();

        Assert.AreEqual(FocusActivationSource.Manual, context.Focus.Current.ActiveState?.Source);
    }

    [TestMethod]
    public void ManualDeactivation_SuppressesTriggerUntilItLeavesAndReturns()
    {
        var profile = Profile(
            "automatic",
            rules:
            [
                new FocusActivationRule(
                    "fg",
                    true,
                    FocusActivationRuleKind.ApplicationForeground,
                    "game.exe")
            ]);
        var context = CreateContext([profile]);
        context.Applications.Set(
            "game.exe",
            ["game.exe"],
            processMonitoringAvailable: true);
        using var service = context.CreateService();
        service.Start();
        Assert.IsTrue(context.Focus.Current.IsActive);

        context.Focus.Deactivate();
        context.Applications.PublishCurrent();

        Assert.IsFalse(context.Focus.Current.IsActive);

        context.Applications.Set(null, ["game.exe"], true);
        context.Applications.Set("game.exe", ["game.exe"], true);

        Assert.IsTrue(context.Focus.Current.IsActive);
        Assert.AreEqual("automatic", context.Focus.Current.ActiveProfile?.Id);
    }

    [TestMethod]
    public void RunningRule_IsIgnoredWhenProcessMonitoringUnavailable()
    {
        var profile = Profile(
            "running",
            rules:
            [
                new FocusActivationRule(
                    "run",
                    true,
                    FocusActivationRuleKind.ApplicationRunning,
                    "music.exe")
            ]);
        var context = CreateContext([profile]);
        context.Applications.Set(
            null,
            ["music.exe"],
            processMonitoringAvailable: false);
        using var service = context.CreateService();

        service.Start();

        Assert.IsFalse(context.Focus.Current.IsActive);
    }

    [TestMethod]
    public void DisabledFocus_DoesNotStartWatchersOrEvaluateAutomation()
    {
        var profile = Profile(
            "automatic",
            rules:
            [
                new FocusActivationRule(
                    "fg",
                    true,
                    FocusActivationRuleKind.ApplicationForeground,
                    "game.exe")
            ]);
        var context = CreateContext([profile]);
        context.Applications.Set("game.exe", ["game.exe"], true);
        context.Settings.Current = context.Settings.Current with
        {
            Focus = context.Settings.Current.Focus with { IsEnabled = false }
        };
        using var service = context.CreateService();

        service.Start();

        Assert.IsFalse(context.Focus.Current.IsActive);
        Assert.AreEqual(0, context.Applications.StartCount);
        Assert.AreEqual(0, context.Applications.SubscriberCount);
        Assert.AreEqual(0, context.Fullscreen.StartCount);
        Assert.AreEqual(0, context.Resume.StartCount);
    }

    [TestMethod]
    public void ToggleFocus_ReattachesWatchersExactlyOnceAndKeepsProfiles()
    {
        var profile = Profile("automatic");
        var context = CreateContext([profile]);
        context.Settings.Current = context.Settings.Current with
        {
            Focus = context.Settings.Current.Focus with { IsEnabled = false }
        };
        using var service = context.CreateService();
        service.Start();

        context.Settings.Update(value => value with
        {
            Focus = value.Focus with { IsEnabled = true }
        });
        context.Settings.Update(value => value with
        {
            Focus = value.Focus with { IsEnabled = true }
        });

        Assert.AreEqual(1, context.Applications.StartCount);
        Assert.AreEqual(1, context.Applications.SubscriberCount);
        Assert.AreEqual(1, context.Fullscreen.StartCount);
        Assert.AreEqual(1, context.Fullscreen.SubscriberCount);
        Assert.AreEqual(1, context.Resume.StartCount);
        Assert.AreEqual(1, context.Resume.SubscriberCount);
        Assert.IsTrue(context.Settings.Current.Focus.Profiles.Any(item => item.Id == profile.Id));

        context.Settings.Update(value => value with
        {
            Focus = value.Focus with { IsEnabled = false }
        });

        Assert.AreEqual(0, context.Applications.SubscriberCount);
        Assert.AreEqual(0, context.Fullscreen.SubscriberCount);
        Assert.AreEqual(0, context.Resume.SubscriberCount);
    }

    private static TestContext CreateContext(IReadOnlyList<FocusProfile> profiles)
    {
        var settings = new FakeSettingsService
        {
            Current = MiaDockSettings.Default with
            {
                Focus = FocusSettings.Default with { Profiles = profiles }
            }
        };
        return new TestContext(
            settings,
            new FakeFocusService(settings),
            new FakeApplicationActivityService(),
            new FakeFullscreenService(),
            new FakeResumeService());
    }

    private static FocusProfile Profile(
        string id,
        IReadOnlyList<FocusSchedule>? schedules = null,
        IReadOnlyList<FocusActivationRule>? rules = null) =>
        new(
            id,
            FocusProfileKind.Custom,
            id,
            "star",
            "#0EA5E9",
            null,
            FocusProfileBehavior.Default,
            schedules ?? Array.Empty<FocusSchedule>(),
            rules ?? Array.Empty<FocusActivationRule>());

    private sealed record TestContext(
        FakeSettingsService Settings,
        FakeFocusService Focus,
        FakeApplicationActivityService Applications,
        FakeFullscreenService Fullscreen,
        FakeResumeService Resume)
    {
        public FocusAutomationService CreateService() =>
            new(
                Settings,
                Focus,
                Applications,
                Fullscreen,
                Resume,
                new ImmediateDispatcher(),
                new FrozenTimeProvider(TestNow),
                TimeZoneInfo.Utc);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public MiaDockSettings Current { get; set; } = MiaDockSettings.Default;
        public Exception? LastSaveFailure => null;
        public string SettingsFilePath => string.Empty;
        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Update(Func<MiaDockSettings, MiaDockSettings> update)
        {
            var previous = Current;
            Current = SettingsValidator.Normalize(update(Current));
            if (Current != previous)
            {
                SettingsChanged?.Invoke(
                    this,
                    new SettingsChangedEventArgs(previous, Current));
            }
        }

        public void Reset() => Current = MiaDockSettings.Default;
        public Task FlushAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeFocusService : IFocusService
    {
        private readonly FakeSettingsService _settings;

        public FakeFocusService(FakeSettingsService settings)
        {
            _settings = settings;
            Current = Snapshot();
        }

        public FocusSnapshot Current { get; private set; }
        public event EventHandler<FocusChangedEventArgs>? FocusChanged;
        public void Start() { }

        public bool Activate(
            string profileId,
            FocusActivationSource source = FocusActivationSource.Manual) =>
            Set(profileId, source);

        public bool ActivateFor(
            string profileId,
            TimeSpan duration,
            FocusActivationSource source = FocusActivationSource.Manual) =>
            Set(profileId, source);

        public bool ActivateIndefinitely(
            string profileId,
            FocusActivationSource source = FocusActivationSource.Manual) =>
            Set(profileId, source);

        public bool Deactivate()
        {
            if (!Current.IsActive)
            {
                return false;
            }

            var previous = Current;
            Current = new FocusSnapshot(_settings.Current.Focus.Profiles, null, null);
            FocusChanged?.Invoke(
                this,
                new FocusChangedEventArgs(
                    previous,
                    Current,
                    FocusChangeReason.Deactivated));
            return true;
        }

        public bool Refresh() => false;
        public void Dispose() { }

        public void SetManual(string profileId) =>
            Set(profileId, FocusActivationSource.Manual);

        private bool Set(string profileId, FocusActivationSource source)
        {
            var profile = _settings.Current.Focus.Profiles.First(item => item.Id == profileId);
            var previous = Current;
            Current = new FocusSnapshot(
                _settings.Current.Focus.Profiles,
                profile,
                new FocusActivationState(
                    profileId,
                    source,
                    TestNow,
                    null));
            FocusChanged?.Invoke(
                this,
                new FocusChangedEventArgs(
                    previous,
                    Current,
                    previous.IsActive
                        ? FocusChangeReason.Switched
                        : FocusChangeReason.Activated));
            return true;
        }

        private FocusSnapshot Snapshot() =>
            new(_settings.Current.Focus.Profiles, null, null);
    }

    private sealed class FakeApplicationActivityService : IApplicationActivityService
    {
        private EventHandler<ApplicationActivitySnapshot>? _activityChanged;

        public ApplicationActivitySnapshot Current { get; private set; } =
            ApplicationActivitySnapshot.Empty;
        public Exception? LastFailure => null;
        public int StartCount { get; private set; }
        public int SubscriberCount => _activityChanged?.GetInvocationList().Length ?? 0;
        public event EventHandler<ApplicationActivitySnapshot>? ActivityChanged
        {
            add => _activityChanged += value;
            remove => _activityChanged -= value;
        }
        public void Start() => StartCount++;
        public void Refresh() { }
        public void Dispose() { }

        public void Set(
            string? foreground,
            IEnumerable<string> running,
            bool processMonitoringAvailable)
        {
            Current = new ApplicationActivitySnapshot(
                foreground,
                running.ToHashSet(StringComparer.OrdinalIgnoreCase),
                Array.Empty<FocusApplicationInfo>(),
                processMonitoringAvailable);
            _activityChanged?.Invoke(this, Current);
        }

        public void PublishCurrent() => _activityChanged?.Invoke(this, Current);
    }

    private sealed class FakeFullscreenService : IFullscreenDetectionService
    {
        private EventHandler<FullscreenSnapshot>? _stateChanged;

        public FullscreenSnapshot Current { get; private set; } = FullscreenSnapshot.None;
        public Exception? LastFailure => null;
        public int StartCount { get; private set; }
        public int SubscriberCount => _stateChanged?.GetInvocationList().Length ?? 0;
        public event EventHandler<FullscreenSnapshot>? StateChanged
        {
            add => _stateChanged += value;
            remove => _stateChanged -= value;
        }
        public void Start() => StartCount++;
        public void Refresh() { }
        public void Dispose() { }
    }

    private sealed class FakeResumeService : ISystemResumeService
    {
        private EventHandler? _resumed;

        public int StartCount { get; private set; }
        public int SubscriberCount => _resumed?.GetInvocationList().Length ?? 0;
        public event EventHandler? Resumed
        {
            add => _resumed += value;
            remove => _resumed -= value;
        }
        public void Start() => StartCount++;
        public void Dispose() { }
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public bool HasThreadAccess => true;
        public bool TryEnqueue(Action callback)
        {
            callback();
            return true;
        }
    }

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period) =>
            new InertTimer();
    }

    private sealed class InertTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
