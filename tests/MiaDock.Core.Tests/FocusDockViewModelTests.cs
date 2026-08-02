using MiaDock.App.ViewModels;
using MiaDock.Core.Focus;
using MiaDock.Core.Localization;
using MiaDock.Core.Settings;
using MiaDock.Core.Threading;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class FocusDockViewModelTests
{
    [TestMethod]
    public void InitialState_ShowsLocalizedBuiltInProfilesAndInactiveStatus()
    {
        var focus = new FakeFocusService();
        var localization = Localizer();
        using var viewModel = new FocusDockViewModel(
            focus,
            localization,
            new ImmediateDispatcher());

        Assert.HasCount(4, viewModel.Profiles);
        Assert.HasCount(4, viewModel.QuickProfiles);
        Assert.AreEqual("Çalışma", viewModel.Profiles[0].DisplayName);
        Assert.AreEqual("Odak kapalı", viewModel.StatusText);
        Assert.IsFalse(viewModel.IsActive);

        localization.SetLanguage(AppLanguage.English);

        Assert.AreEqual("Work", viewModel.Profiles[0].DisplayName);
        Assert.AreEqual("Focus is off", viewModel.StatusText);
    }

    [TestMethod]
    public void CustomProfileName_IsNeverTranslated()
    {
        var custom = new FocusProfile(
            "writing",
            FocusProfileKind.Custom,
            "Deep Writing",
            "book",
            "#22C55E",
            null,
            FocusProfileBehavior.Default,
            Array.Empty<FocusSchedule>(),
            Array.Empty<FocusActivationRule>());
        var focus = new FakeFocusService(
            new FocusSnapshot(
                [.. FocusProfileDefaults.All, custom],
                null,
                null));
        var localization = Localizer();
        using var viewModel = new FocusDockViewModel(
            focus,
            localization,
            new ImmediateDispatcher());

        localization.SetLanguage(AppLanguage.English);

        Assert.AreEqual(
            "Deep Writing",
            viewModel.Profiles.Single(item => item.Id == custom.Id).DisplayName);
    }

    [TestMethod]
    public void Commands_ActivateChangeDurationAndExplicitlyDeactivate()
    {
        var focus = new FakeFocusService();
        using var viewModel = new FocusDockViewModel(
            focus,
            Localizer(),
            new ImmediateDispatcher(),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-07-29T12:00:00Z")));

        viewModel.ActivateProfileCommand.Execute(FocusProfileDefaults.WorkId);

        Assert.IsTrue(viewModel.IsActive);
        Assert.AreEqual("Çalışma", viewModel.ActiveProfileName);
        Assert.AreEqual(1, focus.ActivateCount);

        viewModel.ActivateProfileCommand.Execute(FocusProfileDefaults.WorkId);
        Assert.IsTrue(viewModel.IsActive);
        Assert.AreEqual(1, focus.ActivateCount);
        Assert.AreEqual(0, focus.DeactivateCount);

        viewModel.SetDurationCommand.Execute("30");
        Assert.AreEqual(TimeSpan.FromMinutes(30), focus.LastDuration);
        Assert.AreEqual("30 dk kaldı", viewModel.RemainingText);

        viewModel.SetDurationCommand.Execute("indefinite");
        Assert.IsNull(focus.LastDuration);
        Assert.AreEqual("Kapatılana kadar", viewModel.RemainingText);

        viewModel.DeactivateCommand.Execute(null);
        Assert.IsFalse(viewModel.IsActive);
        Assert.AreEqual(1, focus.DeactivateCount);
    }

    [TestMethod]
    public void QuickProfiles_ShowOnlyFirstFourAndDisableActiveProfile()
    {
        var custom = new FocusProfile(
            "writing", FocusProfileKind.Custom, "Deep Writing", "book", "#22C55E",
            null, FocusProfileBehavior.Default, [], []);
        var focus = new FakeFocusService(new FocusSnapshot(
            [.. FocusProfileDefaults.All, custom], null, null));
        using var viewModel = new FocusDockViewModel(
            focus, Localizer(), new ImmediateDispatcher());

        Assert.HasCount(5, viewModel.Profiles);
        Assert.HasCount(4, viewModel.QuickProfiles);
        Assert.IsFalse(viewModel.QuickProfiles.Any(item => item.Id == custom.Id));

        viewModel.ActivateProfileCommand.Execute(FocusProfileDefaults.WorkId);

        Assert.IsFalse(viewModel.QuickProfiles[0].CanActivate);
        Assert.IsTrue(viewModel.QuickProfiles.Skip(1).All(item => item.CanActivate));
    }

    private static TestLocalizationService Localizer() =>
        new(new Dictionary<string, (string Turkish, string English)>
        {
            ["Focus.Title"] = ("Odak", "Focus"),
            ["Focus.Profile.Work.Name"] = ("Çalışma", "Work"),
            ["Focus.Profile.Gaming.Name"] = ("Oyun", "Gaming"),
            ["Focus.Profile.Sleep.Name"] = ("Uyku", "Sleep"),
            ["Focus.Profile.DoNotDisturb.Name"] = ("Rahatsız Etmeyin", "Do Not Disturb"),
            ["Focus.Profile.Custom.Name"] = ("Özel Odak", "Custom Focus"),
            ["Focus.Status.Off"] = ("Odak kapalı", "Focus is off"),
            ["Focus.Status.Active"] = ("{0} · {1}", "{0} · {1}"),
            ["Focus.Duration"] = ("Süreyi değiştir", "Change duration"),
            ["Focus.Duration.15Minutes"] = ("15 dakika", "15 minutes"),
            ["Focus.Duration.30Minutes"] = ("30 dakika", "30 minutes"),
            ["Focus.Duration.1Hour"] = ("1 saat", "1 hour"),
            ["Focus.Duration.2Hours"] = ("2 saat", "2 hours"),
            ["Focus.Duration.UntilTurnedOff"] = ("Kapatılana kadar", "Until turned off"),
            ["Focus.Duration.MinutesRemaining"] = ("{0} dk kaldı", "{0} min remaining"),
            ["Focus.TurnOff"] = ("Odağı kapat", "Turn off Focus")
        });

    private sealed class FakeFocusService : IFocusService
    {
        private static readonly DateTimeOffset Now =
            DateTimeOffset.Parse("2026-07-29T12:00:00Z");

        public FakeFocusService(FocusSnapshot? current = null)
        {
            Current = current ?? new FocusSnapshot(
                FocusProfileDefaults.All,
                null,
                null);
        }

        public FocusSnapshot Current { get; private set; }
        public int ActivateCount { get; private set; }
        public int DeactivateCount { get; private set; }
        public TimeSpan? LastDuration { get; private set; }
        public event EventHandler<FocusChangedEventArgs>? FocusChanged;

        public void Start()
        {
        }

        public bool Activate(
            string profileId,
            FocusActivationSource source = FocusActivationSource.Manual)
        {
            ActivateCount++;
            return SetActive(profileId, null, source);
        }

        public bool ActivateFor(
            string profileId,
            TimeSpan duration,
            FocusActivationSource source = FocusActivationSource.Manual)
        {
            LastDuration = duration;
            return SetActive(profileId, duration, source);
        }

        public bool ActivateIndefinitely(
            string profileId,
            FocusActivationSource source = FocusActivationSource.Manual)
        {
            LastDuration = null;
            return SetActive(profileId, null, source);
        }

        public bool Deactivate()
        {
            if (!Current.IsActive)
            {
                return false;
            }

            DeactivateCount++;
            var previous = Current;
            Current = Current with { ActiveProfile = null, ActiveState = null };
            FocusChanged?.Invoke(
                this,
                new FocusChangedEventArgs(
                    previous,
                    Current,
                    FocusChangeReason.Deactivated));
            return true;
        }

        public bool Refresh() => false;

        public void Dispose()
        {
        }

        private bool SetActive(
            string profileId,
            TimeSpan? duration,
            FocusActivationSource source)
        {
            var profile = Current.Profiles.Single(item => item.Id == profileId);
            var previous = Current;
            Current = Current with
            {
                ActiveProfile = profile,
                ActiveState = new FocusActivationState(
                    profileId,
                    source,
                    Now,
                    duration is { } value ? Now.Add(value) : null)
            };
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
