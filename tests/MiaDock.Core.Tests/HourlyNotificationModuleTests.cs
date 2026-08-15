using System.Globalization;
using MiaDock.App.Modules;
using MiaDock.App.Services;
using MiaDock.Core.Localization;
using MiaDock.Core.Modules;
using MiaDock.Core.Settings;
using MiaDock.Core.Threading;
using MiaDock.Modules.Time.Services;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class HourlyNotificationModuleTests
{
    [TestMethod]
    public async Task Activate_FirstSnapshotIsSilent_ThenPublishesOnceAtNextLocalHour()
    {
        var time = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.Zero),
            TimeZoneInfo.CreateCustomTimeZone(
                "Test UTC+3",
                TimeSpan.FromHours(3),
                "Test UTC+3",
                "Test UTC+3"));
        var resume = new FakeResumeService();
        using var module = CreateModule(time, resume, ClockHourFormat.TwentyFourHour);
        var events = new List<ModuleEvent>();
        module.EventOccurred += (_, value) => events.Add(value);
        module.IsEnabled = true;

        await module.ActivateAsync();

        Assert.IsEmpty(events);
        time.Advance(TimeSpan.FromMinutes(30));

        Assert.HasCount(1, events);
        Assert.AreEqual("Saat 14:00", events[0].Presentation.PrimaryText);
        Assert.AreEqual("Saat başı bildirimi", events[0].Presentation.SecondaryText);
        Assert.AreEqual(ModuleEventKind.Notification, events[0].Kind);
        Assert.AreEqual(ModuleEventPriority.Normal, events[0].Priority);
        Assert.IsFalse(events[0].IsFullscreenEligible);
        Assert.AreEqual(AudibleNotificationCue.Hourly, events[0].AudibleCue);

        time.FireDueTimers();
        Assert.HasCount(1, events);
    }

    [TestMethod]
    public async Task TwelveHourPreference_FormatsHourlyPresentationWithoutSeconds()
    {
        var time = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 14, 11, 59, 30, TimeSpan.Zero),
            TimeZoneInfo.Utc);
        using var module = CreateModule(time, new FakeResumeService(), ClockHourFormat.TwelveHour);
        ModuleEvent? raised = null;
        module.EventOccurred += (_, value) => raised = value;
        module.IsEnabled = true;
        await module.ActivateAsync();

        time.Advance(TimeSpan.FromSeconds(30));

        Assert.IsNotNull(raised);
        Assert.AreEqual("Saat 12:00 PM", raised.Presentation.PrimaryText);
    }

    [TestMethod]
    public async Task LateTimer_DoesNotCatchUpAndSchedulesTheNextRealHour()
    {
        var time = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.Zero),
            TimeZoneInfo.Utc);
        using var module = CreateModule(time, new FakeResumeService(), ClockHourFormat.TwentyFourHour);
        var events = new List<ModuleEvent>();
        module.EventOccurred += (_, value) => events.Add(value);
        module.IsEnabled = true;
        await module.ActivateAsync();

        time.Advance(TimeSpan.FromMinutes(91));
        Assert.IsEmpty(events);

        time.Advance(TimeSpan.FromMinutes(59));
        Assert.HasCount(1, events);
        Assert.AreEqual("Saat 13:00", events[0].Presentation.PrimaryText);
    }

    [TestMethod]
    public async Task Resume_RearmsWithoutPublishingMissedHours()
    {
        var time = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.Zero),
            TimeZoneInfo.Utc);
        var resume = new FakeResumeService();
        using var module = CreateModule(time, resume, ClockHourFormat.TwentyFourHour);
        var events = new List<ModuleEvent>();
        module.EventOccurred += (_, value) => events.Add(value);
        module.IsEnabled = true;
        await module.ActivateAsync();

        time.SetUtcNowWithoutFiring(new DateTimeOffset(2026, 8, 14, 12, 5, 0, TimeSpan.Zero));
        resume.RaiseResumed();
        time.FireDueTimers();

        Assert.IsEmpty(events);
        time.Advance(TimeSpan.FromMinutes(55));
        Assert.HasCount(1, events);
        Assert.AreEqual("Saat 13:00", events[0].Presentation.PrimaryText);
    }

    [TestMethod]
    public async Task TimeZoneChange_RecalculatesTheNextLocalHour()
    {
        var time = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 14, 10, 10, 0, TimeSpan.Zero),
            TimeZoneInfo.Utc);
        using var module = CreateModule(time, new FakeResumeService(), ClockHourFormat.TwentyFourHour);
        var events = new List<ModuleEvent>();
        module.EventOccurred += (_, value) => events.Add(value);
        module.IsEnabled = true;
        await module.ActivateAsync();

        time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone(
            "Test UTC+0:30",
            TimeSpan.FromMinutes(30),
            "Test UTC+0:30",
            "Test UTC+0:30"));
        time.Advance(TimeSpan.FromMinutes(10));
        Assert.IsEmpty(events);

        time.Advance(TimeSpan.FromMinutes(10));
        Assert.HasCount(1, events);
        Assert.AreEqual("Saat 11:00", events[0].Presentation.PrimaryText);
    }

    [TestMethod]
    public async Task Deactivate_CancelsThePendingBoundary()
    {
        var time = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.Zero),
            TimeZoneInfo.Utc);
        using var module = CreateModule(time, new FakeResumeService(), ClockHourFormat.TwentyFourHour);
        var eventCount = 0;
        module.EventOccurred += (_, _) => eventCount++;
        module.IsEnabled = true;
        await module.ActivateAsync();

        await module.DeactivateAsync();
        time.Advance(TimeSpan.FromHours(2));

        Assert.AreEqual(0, eventCount);
        Assert.AreEqual(0, time.ActiveTimerCount);
    }

    private static HourlyNotificationModule CreateModule(
        ManualTimeProvider time,
        FakeResumeService resume,
        ClockHourFormat hourFormat)
    {
        var settings = MiaDockSettings.Default with
        {
            General = MiaDockSettings.Default.General with
            {
                Clock = MiaDockSettings.Default.General.Clock with
                {
                    HourFormat = hourFormat,
                    ShowSeconds = true
                }
            }
        };
        return new HourlyNotificationModule(
            new FakeSettingsService(settings),
            new FakeLocalizationService(),
            new ImmediateDispatcher(),
            resume,
            time);
    }

    private sealed class FakeSettingsService(MiaDockSettings settings) : ISettingsService
    {
        public MiaDockSettings Current { get; private set; } = settings;
        public Exception? LastSaveFailure => null;
        public string SettingsFilePath => string.Empty;
        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Func<MiaDockSettings, MiaDockSettings> update)
        {
            var previous = Current;
            Current = update(Current);
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(previous, Current));
        }
        public void Reset() => Update(_ => MiaDockSettings.Default);
        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public AppLanguage CurrentLanguage => AppLanguage.Turkish;
        public CultureInfo CurrentCulture { get; } = new("en-US");
        public event EventHandler? LanguageChanged { add { } remove { } }
        public void SetLanguage(AppLanguage language) { }
        public string Get(string key, params object?[] arguments) => key switch
        {
            "HourlyNotification.TimeFormat" => string.Format(CurrentCulture, "Saat {0}", arguments),
            "HourlyNotification.Description" => "Saat başı bildirimi",
            _ => key
        };
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

    private sealed class FakeResumeService : ISystemResumeService
    {
        public event EventHandler? Resumed;
        public void Start() { }
        public void RaiseResumed() => Resumed?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];

        public ManualTimeProvider(DateTimeOffset utcNow, TimeZoneInfo timeZone)
        {
            UtcNow = utcNow;
            LocalTimeZoneValue = timeZone;
        }

        public DateTimeOffset UtcNow { get; private set; }
        private TimeZoneInfo LocalTimeZoneValue { get; set; }
        public int ActiveTimerCount => _timers.Count(timer => !timer.IsDisposed);
        public override TimeZoneInfo LocalTimeZone => LocalTimeZoneValue;
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
            FireDueTimers();
        }

        public void SetUtcNowWithoutFiring(DateTimeOffset value) => UtcNow = value;

        public void SetLocalTimeZone(TimeZoneInfo value) => LocalTimeZoneValue = value;

        public void FireDueTimers()
        {
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
                if (IsDisposed || _dueAtUtc is not { } dueAtUtc || dueAtUtc > _owner.UtcNow)
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
