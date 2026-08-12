using MiaDock.App.Services;
using MiaDock.Core.Focus;
using MiaDock.Core.Settings;
using MiaDock.Core.Threading;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class FocusServiceTests
{
    private static readonly DateTimeOffset TestNow =
        DateTimeOffset.Parse("2026-07-29T12:00:00Z");

    [TestMethod]
    public void Start_RestoresPersistedActiveProfileAndRaisesInitialized()
    {
        var time = new ManualTimeProvider(TestNow);
        var settings = new FakeSettingsService
        {
            Current = WithActiveState(
                MiaDockSettings.Default,
                FocusProfileDefaults.WorkId,
                TestNow.AddMinutes(-5),
                TestNow.AddMinutes(25))
        };
        using var service = CreateService(settings, time);
        FocusChangedEventArgs? change = null;
        service.FocusChanged += (_, args) => change = args;

        service.Start();

        Assert.IsTrue(service.Current.IsActive);
        Assert.AreEqual(FocusProfileDefaults.WorkId, service.Current.ActiveProfile?.Id);
        Assert.AreEqual(FocusChangeReason.Initialized, change?.Reason);
    }

    [TestMethod]
    public void Start_ClearsStateThatExpiredWhileApplicationWasClosed()
    {
        var time = new ManualTimeProvider(TestNow);
        var settings = new FakeSettingsService
        {
            Current = WithActiveState(
                MiaDockSettings.Default,
                FocusProfileDefaults.WorkId,
                TestNow.AddHours(-1),
                TestNow.AddMinutes(-1))
        };
        using var service = CreateService(settings, time);
        FocusChangedEventArgs? change = null;
        service.FocusChanged += (_, args) => change = args;

        service.Start();

        Assert.IsFalse(service.Current.IsActive);
        Assert.IsNull(settings.Current.Focus.ActiveState);
        Assert.AreEqual(FocusChangeReason.Expired, change?.Reason);
    }

    [TestMethod]
    public void Activate_UsesProfileDefaultDurationAndPersistsUtcState()
    {
        var time = new ManualTimeProvider(TestNow.ToOffset(TimeSpan.FromHours(3)));
        var custom = CreateCustom("reading", defaultDurationMinutes: 25);
        var settings = new FakeSettingsService
        {
            Current = WithCustomProfile(MiaDockSettings.Default, custom)
        };
        using var service = CreateService(settings, time);
        service.Start();
        var changes = new List<FocusChangedEventArgs>();
        service.FocusChanged += (_, args) => changes.Add(args);

        var activated = service.Activate("reading");

        Assert.IsTrue(activated);
        Assert.AreEqual("reading", settings.Current.Focus.ActiveState?.ProfileId);
        Assert.AreEqual(TimeSpan.Zero, settings.Current.Focus.ActiveState?.StartedAtUtc.Offset);
        Assert.AreEqual(TestNow, settings.Current.Focus.ActiveState?.StartedAtUtc);
        Assert.AreEqual(TestNow.AddMinutes(25), settings.Current.Focus.ActiveState?.EndsAtUtc);
        Assert.HasCount(1, changes);
        Assert.AreEqual(FocusChangeReason.Activated, changes[0].Reason);
    }

    [TestMethod]
    public void ActivateForAndIndefinitely_ApplyRequestedDurationModes()
    {
        var time = new ManualTimeProvider(TestNow);
        var settings = new FakeSettingsService();
        using var service = CreateService(settings, time);
        service.Start();

        Assert.IsTrue(service.ActivateFor(
            FocusProfileDefaults.GamingId,
            TimeSpan.FromHours(2)));
        Assert.AreEqual(TestNow.AddHours(2), service.Current.ActiveState?.EndsAtUtc);

        Assert.IsTrue(service.ActivateIndefinitely(FocusProfileDefaults.GamingId));
        Assert.IsNull(service.Current.ActiveState?.EndsAtUtc);
    }

    [TestMethod]
    public void Activate_RejectsUnknownProfileInvalidSourceAndOutOfRangeDurations()
    {
        var settings = new FakeSettingsService();
        using var service = CreateService(settings, new ManualTimeProvider(TestNow));
        service.Start();
        var initial = settings.Current.Focus.ActiveState;

        Assert.IsFalse(service.Activate("missing"));
        Assert.IsFalse(service.Activate(""));
        Assert.IsFalse(service.ActivateFor(
            FocusProfileDefaults.WorkId,
            TimeSpan.FromSeconds(59)));
        Assert.IsFalse(service.ActivateFor(
            FocusProfileDefaults.WorkId,
            TimeSpan.FromHours(25)));
        Assert.IsFalse(service.Activate(
            FocusProfileDefaults.WorkId,
            (FocusActivationSource)999));
        Assert.AreEqual(initial, settings.Current.Focus.ActiveState);
    }

    [TestMethod]
    public void DisabledFocus_DoesNotRestoreOrActivateAndCreatesNoTimer()
    {
        var time = new ManualTimeProvider(TestNow);
        var settings = new FakeSettingsService
        {
            Current = WithActiveState(
                MiaDockSettings.Default,
                FocusProfileDefaults.WorkId,
                TestNow.AddMinutes(-5),
                TestNow.AddMinutes(25)) with
            {
                Focus = MiaDockSettings.Default.Focus with
                {
                    IsEnabled = false,
                    ActiveState = new FocusActivationState(
                        FocusProfileDefaults.WorkId,
                        FocusActivationSource.Manual,
                        TestNow.AddMinutes(-5),
                        TestNow.AddMinutes(25))
                }
            }
        };
        using var service = CreateService(settings, time);

        service.Start();

        Assert.IsFalse(service.Current.IsActive);
        Assert.IsFalse(service.Activate(FocusProfileDefaults.WorkId));
        Assert.AreEqual(0, time.ActiveTimerCount);
    }

    [TestMethod]
    public void ReenabledFocus_PreservesProfilesButDoesNotRestorePreviousActivation()
    {
        var custom = CreateCustom("reading", defaultDurationMinutes: 25);
        var settings = new FakeSettingsService
        {
            Current = WithCustomProfile(MiaDockSettings.Default, custom) with
            {
                Focus = WithCustomProfile(MiaDockSettings.Default, custom).Focus with
                {
                    IsEnabled = false
                }
            }
        };
        using var service = CreateService(settings, new ManualTimeProvider(TestNow));
        service.Start();

        settings.Update(value => value with
        {
            Focus = value.Focus with { IsEnabled = true }
        });

        Assert.IsFalse(service.Current.IsActive);
        Assert.IsTrue(service.Current.Profiles.Any(profile => profile.Id == custom.Id));
        Assert.IsTrue(service.Activate(custom.Id));
    }

    [TestMethod]
    public void Activate_DifferentProfileRaisesSwitched()
    {
        var settings = new FakeSettingsService();
        using var service = CreateService(settings, new ManualTimeProvider(TestNow));
        service.Start();
        service.Activate(FocusProfileDefaults.WorkId);
        FocusChangedEventArgs? change = null;
        service.FocusChanged += (_, args) => change = args;

        var switched = service.Activate(FocusProfileDefaults.GamingId);

        Assert.IsTrue(switched);
        Assert.AreEqual(FocusProfileDefaults.GamingId, service.Current.ActiveProfile?.Id);
        Assert.AreEqual(FocusChangeReason.Switched, change?.Reason);
    }

    [TestMethod]
    public void ExpirationTimer_DeactivatesFocusWithoutPolling()
    {
        var time = new ManualTimeProvider(TestNow);
        var settings = new FakeSettingsService();
        using var service = CreateService(settings, time);
        service.Start();
        service.ActivateFor(FocusProfileDefaults.WorkId, TimeSpan.FromMinutes(10));
        FocusChangedEventArgs? change = null;
        service.FocusChanged += (_, args) => change = args;

        time.Advance(TimeSpan.FromMinutes(10));

        Assert.IsFalse(service.Current.IsActive);
        Assert.IsNull(settings.Current.Focus.ActiveState);
        Assert.AreEqual(FocusChangeReason.Expired, change?.Reason);
    }

    [TestMethod]
    public void Deactivate_StopsFocusAndDoesNotPublishDuplicateChange()
    {
        var settings = new FakeSettingsService();
        using var service = CreateService(settings, new ManualTimeProvider(TestNow));
        service.Start();
        service.ActivateIndefinitely(FocusProfileDefaults.WorkId);
        var changes = new List<FocusChangedEventArgs>();
        service.FocusChanged += (_, args) => changes.Add(args);

        Assert.IsTrue(service.Deactivate());
        Assert.IsFalse(service.Deactivate());

        Assert.HasCount(1, changes);
        Assert.AreEqual(FocusChangeReason.Deactivated, changes[0].Reason);
    }

    [TestMethod]
    public void RemovingActiveCustomProfileSafelyDeactivatesFocus()
    {
        var custom = CreateCustom("reading", defaultDurationMinutes: null);
        var settings = new FakeSettingsService
        {
            Current = WithCustomProfile(MiaDockSettings.Default, custom)
        };
        using var service = CreateService(settings, new ManualTimeProvider(TestNow));
        service.Start();
        service.ActivateIndefinitely(custom.Id);
        FocusChangedEventArgs? change = null;
        service.FocusChanged += (_, args) => change = args;

        settings.Update(value => value with
        {
            Focus = value.Focus with
            {
                Profiles = value.Focus.Profiles
                    .Where(profile => profile.Id != custom.Id)
                    .ToArray()
            }
        });

        Assert.IsFalse(service.Current.IsActive);
        Assert.AreEqual(FocusChangeReason.ProfileRemoved, change?.Reason);
    }

    [TestMethod]
    public void UnrelatedSettingsChangeDoesNotPublishFocusEvent()
    {
        var settings = new FakeSettingsService();
        using var service = CreateService(settings, new ManualTimeProvider(TestNow));
        service.Start();
        var changeCount = 0;
        service.FocusChanged += (_, _) => changeCount++;

        settings.Update(value => value with
        {
            Appearance = value.Appearance with { AccentColor = "#123456" }
        });

        Assert.AreEqual(0, changeCount);
    }

    [TestMethod]
    public void Dispose_UnsubscribesFromSettingsAndCancelsTimer()
    {
        var time = new ManualTimeProvider(TestNow);
        var settings = new FakeSettingsService();
        var service = CreateService(settings, time);
        service.Start();
        service.ActivateFor(FocusProfileDefaults.WorkId, TimeSpan.FromMinutes(10));
        var snapshot = service.Current;

        service.Dispose();
        settings.Update(value => value with
        {
            Focus = value.Focus with { ActiveState = null }
        });
        time.Advance(TimeSpan.FromMinutes(10));

        Assert.AreSame(snapshot, service.Current);
        Assert.AreEqual(0, time.ActiveTimerCount);
    }

    private static FocusService CreateService(
        FakeSettingsService settings,
        ManualTimeProvider time) =>
        new(settings, new ImmediateDispatcher(), time);

    private static MiaDockSettings WithActiveState(
        MiaDockSettings settings,
        string profileId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? endsAtUtc) =>
        settings with
        {
            Focus = settings.Focus with
            {
                ActiveState = new FocusActivationState(
                    profileId,
                    FocusActivationSource.Manual,
                    startedAtUtc,
                    endsAtUtc)
            }
        };

    private static MiaDockSettings WithCustomProfile(
        MiaDockSettings settings,
        FocusProfile profile) =>
        settings with
        {
            Focus = settings.Focus with
            {
                Profiles = [.. settings.Focus.Profiles, profile]
            }
        };

    private static FocusProfile CreateCustom(
        string id,
        int? defaultDurationMinutes) =>
        new(
            id,
            FocusProfileKind.Custom,
            "Reading",
            "book",
            "#22C55E",
            defaultDurationMinutes,
            FocusProfileBehavior.Default,
            Array.Empty<FocusSchedule>(),
            Array.Empty<FocusActivationRule>());

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
            var current = SettingsValidator.Normalize(update(previous));
            if (current == previous)
            {
                return;
            }

            Current = current;
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(previous, current));
        }

        public void Reset() => Current = MiaDockSettings.Default;

        public Task FlushAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; private set; }

        public int ActiveTimerCount => _timers.Count(timer => !timer.IsDisposed);

        public override DateTimeOffset GetUtcNow() => UtcNow;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, dueTime, period);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan amount)
        {
            UtcNow = UtcNow.Add(amount);
            foreach (var timer in _timers.ToArray())
            {
                timer.FireIfDue();
            }
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly ManualTimeProvider _owner;
            private readonly TimerCallback _callback;
            private readonly object? _state;
            private DateTimeOffset? _dueAtUtc;
            private TimeSpan _period;

            public ManualTimer(
                ManualTimeProvider owner,
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                _owner = owner;
                _callback = callback;
                _state = state;
                Change(dueTime, period);
            }

            public bool IsDisposed { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (IsDisposed)
                {
                    return false;
                }

                _period = period;
                _dueAtUtc = dueTime == Timeout.InfiniteTimeSpan
                    ? null
                    : _owner.UtcNow.Add(dueTime);
                return true;
            }

            public void Dispose()
            {
                IsDisposed = true;
                _dueAtUtc = null;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void FireIfDue()
            {
                if (IsDisposed ||
                    _dueAtUtc is not { } dueAtUtc ||
                    dueAtUtc > _owner.UtcNow)
                {
                    return;
                }

                _dueAtUtc = _period == Timeout.InfiniteTimeSpan
                    ? null
                    : dueAtUtc.Add(_period);
                _callback(_state);
            }
        }
    }
}
