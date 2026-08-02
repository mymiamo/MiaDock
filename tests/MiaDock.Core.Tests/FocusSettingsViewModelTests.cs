using MiaDock.App.Services;
using MiaDock.App.ViewModels;
using MiaDock.Core.Focus;
using MiaDock.Core.Modules;
using MiaDock.Core.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class FocusSettingsViewModelTests
{
    [TestMethod]
    public void NewCustomProfile_IsSavedWithSafePrivacyDefaults()
    {
        var settings = new FakeSettingsService();
        using var viewModel = CreateViewModel(settings);
        using var editor = viewModel.CreateNewEditor();

        Assert.IsNotNull(editor);
        editor.Name = "Derin Çalışma";
        editor.ColorHex = "#12ABEF";
        editor.HasDefaultDuration = true;
        editor.DefaultDurationMinutes = 45;

        var result = viewModel.Save(editor);

        Assert.AreEqual(FocusProfileSaveResult.Success, result);
        var custom = settings.Current.Focus.Profiles.Single(profile =>
            profile.Kind == FocusProfileKind.Custom);
        Assert.AreEqual("Derin Çalışma", custom.CustomName);
        Assert.AreEqual("#12ABEF", custom.Color);
        Assert.AreEqual(45, custom.DefaultDurationMinutes);
        Assert.IsTrue(custom.Behavior.AllowFullscreenNotifications);
        Assert.IsFalse(custom.Behavior.AllowSensitiveContentInFullscreen);
        Assert.IsFalse(custom.Behavior.AllowSensitiveContentWhenLocked);
        Assert.IsTrue(custom.Behavior.AllowedModuleIds.Count == 0);
    }

    [TestMethod]
    public void DraftChanges_DoNotModifySettingsUntilSaved()
    {
        var settings = new FakeSettingsService();
        using var viewModel = CreateViewModel(settings);
        using var editor = viewModel.CreateEditor(FocusProfileDefaults.WorkId);
        Assert.IsNotNull(editor);
        var original = settings.Current.Focus.Profiles.Single(profile =>
            profile.Id == FocusProfileDefaults.WorkId);

        editor.ColorHex = "#010203";
        editor.DefaultDurationMinutes = 120;

        Assert.AreEqual(
            original,
            settings.Current.Focus.Profiles.Single(profile =>
                profile.Id == FocusProfileDefaults.WorkId));
    }

    [TestMethod]
    public void InvalidDraft_IsRejectedWithoutWritingSettings()
    {
        var settings = new FakeSettingsService();
        using var viewModel = CreateViewModel(settings);
        using var editor = viewModel.CreateNewEditor();
        Assert.IsNotNull(editor);
        var before = settings.Current;

        editor.Name = " ";
        editor.ColorHex = "not-a-color";
        var result = viewModel.Save(editor);

        Assert.AreEqual(FocusProfileSaveResult.Invalid, result);
        Assert.AreEqual(before, settings.Current);
        Assert.IsTrue(editor.HasError);
    }

    [TestMethod]
    public void DuplicateCustomName_IsRejectedCaseInsensitively()
    {
        var settings = new FakeSettingsService();
        using var viewModel = CreateViewModel(settings);
        using var first = viewModel.CreateNewEditor();
        Assert.IsNotNull(first);
        first.Name = "Okuma";
        Assert.AreEqual(FocusProfileSaveResult.Success, viewModel.Save(first));

        using var duplicate = viewModel.CreateNewEditor();
        Assert.IsNotNull(duplicate);
        duplicate.Name = "okuma";

        Assert.AreEqual(
            FocusProfileSaveResult.DuplicateName,
            viewModel.Save(duplicate));
        Assert.HasCount(1, settings.Current.Focus.Profiles.Where(profile =>
            profile.Kind == FocusProfileKind.Custom));
    }

    [TestMethod]
    public void TwelveCustomProfiles_EnforceCreationLimit()
    {
        var customProfiles = Enumerable.Range(0, 12)
            .Select(index => Custom($"custom-{index}", $"Profile {index}"))
            .ToArray();
        var settings = new FakeSettingsService
        {
            Current = MiaDockSettings.Default with
            {
                Focus = FocusSettings.Default with
                {
                    Profiles =
                    [
                        .. FocusProfileDefaults.All,
                        .. customProfiles
                    ]
                }
            }
        };
        using var viewModel = CreateViewModel(settings);

        Assert.IsFalse(viewModel.CanCreateProfile);
        Assert.IsNull(viewModel.CreateNewEditor());
        Assert.HasCount(16, viewModel.Profiles);
    }

    [TestMethod]
    public void BuiltInProfiles_CannotBeDeletedOrRenamedAndCanBeReset()
    {
        var settings = new FakeSettingsService();
        using var viewModel = CreateViewModel(settings);
        using var editor = viewModel.CreateEditor(FocusProfileDefaults.GamingId);
        Assert.IsNotNull(editor);
        editor.Name = "Renamed";
        editor.ColorHex = "#102030";

        Assert.AreEqual(FocusProfileSaveResult.Success, viewModel.Save(editor));
        var changed = settings.Current.Focus.Profiles.Single(profile =>
            profile.Id == FocusProfileDefaults.GamingId);
        Assert.IsNull(changed.CustomName);
        Assert.AreEqual("#102030", changed.Color);
        Assert.AreEqual(
            FocusProfileSaveResult.ProtectedProfile,
            viewModel.Delete(FocusProfileDefaults.GamingId));

        Assert.AreEqual(
            FocusProfileSaveResult.Success,
            viewModel.ResetBuiltIn(FocusProfileDefaults.GamingId));
        Assert.AreEqual(
            FocusProfileDefaults.FindBuiltIn(FocusProfileDefaults.GamingId),
            settings.Current.Focus.Profiles.Single(profile =>
                profile.Id == FocusProfileDefaults.GamingId));
    }

    [TestMethod]
    public void DeletingActiveCustomProfile_ClearsActiveState()
    {
        var custom = Custom("reading", "Reading");
        var settings = new FakeSettingsService
        {
            Current = MiaDockSettings.Default with
            {
                Focus = new FocusSettings(
                    FocusSettings.CurrentSchemaVersion,
                    [.. FocusProfileDefaults.All, custom],
                    new FocusActivationState(
                        custom.Id,
                        FocusActivationSource.Manual,
                        DateTimeOffset.UtcNow,
                        null))
            }
        };
        using var viewModel = CreateViewModel(settings);

        Assert.AreEqual(
            FocusProfileSaveResult.Success,
            viewModel.Delete(custom.Id));
        Assert.IsNull(settings.Current.Focus.ActiveState);
        Assert.IsFalse(settings.Current.Focus.Profiles.Any(profile =>
            profile.Id == custom.Id));
    }

    [TestMethod]
    public void SelectedModuleList_IsPersistedWhenAllModulesIsOff()
    {
        var settings = new FakeSettingsService();
        var modules = new IIslandModule[]
        {
            new FakeModule("media"),
            new FakeModule("timer")
        };
        using var viewModel = CreateViewModel(settings, modules);
        using var editor = viewModel.CreateNewEditor();
        Assert.IsNotNull(editor);
        editor.Name = "Only music";
        editor.AllowAllModules = false;
        foreach (var module in editor.Modules)
        {
            module.IsSelected = module.ModuleId == "media";
        }

        Assert.AreEqual(FocusProfileSaveResult.Success, viewModel.Save(editor));
        CollectionAssert.AreEqual(
            new[] { "media" },
            settings.Current.Focus.Profiles.Single(profile =>
                profile.Kind == FocusProfileKind.Custom)
            .Behavior.AllowedModuleIds.ToArray());
    }

    [TestMethod]
    public void LanguageChange_RefreshesBuiltInProfileNamesWithoutRestart()
    {
        var settings = new FakeSettingsService();
        var localization = Localizer();
        using var viewModel = new FocusSettingsViewModel(
            settings,
            localization,
            Array.Empty<IIslandModule>());

        Assert.AreEqual(
            "Çalışma",
            viewModel.Profiles.Single(profile =>
                profile.Id == FocusProfileDefaults.WorkId).DisplayName);

        localization.SetLanguage(AppLanguage.English);

        Assert.AreEqual(
            "Work",
            viewModel.Profiles.Single(profile =>
                profile.Id == FocusProfileDefaults.WorkId).DisplayName);
    }

    [TestMethod]
    public void ScheduleAndApplicationRule_AreSavedWithProfileDraft()
    {
        var settings = new FakeSettingsService();
        var applications = new FakeApplicationActivityService(
            new ApplicationActivitySnapshot(
                "code.exe",
                new HashSet<string>(["code.exe"], StringComparer.OrdinalIgnoreCase),
                [new FocusApplicationInfo("code.exe", "Visual Studio Code")],
                true));
        using var viewModel = new FocusSettingsViewModel(
            settings,
            Localizer(),
            Array.Empty<IIslandModule>(),
            applications);
        using var editor = viewModel.CreateNewEditor();
        Assert.IsNotNull(editor);
        editor.Name = "Development";
        editor.AddSchedule();
        editor.AddAutomationRule();

        var result = viewModel.Save(editor);

        Assert.AreEqual(FocusProfileSaveResult.Success, result);
        var profile = settings.Current.Focus.Profiles.Single(item =>
            item.CustomName == "Development");
        Assert.HasCount(1, profile.Schedules);
        Assert.AreEqual(FocusDays.Weekdays, profile.Schedules[0].Days);
        Assert.AreEqual(9 * 60, profile.Schedules[0].StartMinute);
        Assert.HasCount(1, profile.ActivationRules);
        Assert.AreEqual("code.exe", profile.ActivationRules[0].Target);
        Assert.AreEqual(
            FocusActivationRuleKind.ApplicationForeground,
            profile.ActivationRules[0].Kind);
    }

    [TestMethod]
    public void ScheduleWithoutSelectedDay_IsRejected()
    {
        var settings = new FakeSettingsService();
        using var viewModel = CreateViewModel(settings);
        using var editor = viewModel.CreateNewEditor();
        Assert.IsNotNull(editor);
        editor.Name = "Invalid schedule";
        editor.AddSchedule();
        var schedule = editor.Schedules.Single();
        schedule.Monday = false;
        schedule.Tuesday = false;
        schedule.Wednesday = false;
        schedule.Thursday = false;
        schedule.Friday = false;

        Assert.AreEqual(
            FocusProfileSaveResult.Invalid,
            viewModel.Save(editor));
        Assert.IsTrue(editor.HasError);
    }

    [TestMethod]
    public async Task WindowsFocusSettingsCommand_UsesTheSafePlatformLauncher()
    {
        var launcher = new FakeFocusSettingsLauncher();
        using var viewModel = new FocusSettingsViewModel(
            new FakeSettingsService(),
            Localizer(),
            Array.Empty<IIslandModule>(),
            null,
            launcher);

        await viewModel.OpenWindowsFocusSettingsCommand.ExecuteAsync(null);

        Assert.AreEqual(1, launcher.OpenCount);
    }

    private static FocusSettingsViewModel CreateViewModel(
        FakeSettingsService settings,
        IEnumerable<IIslandModule>? modules = null) =>
        new(settings, Localizer(), modules);

    private static TestLocalizationService Localizer() =>
        new(new Dictionary<string, (string Turkish, string English)>
        {
            ["Focus.Profile.Work.Name"] = ("Çalışma", "Work"),
            ["Focus.Profile.Gaming.Name"] = ("Oyun", "Gaming"),
            ["Focus.Profile.Sleep.Name"] = ("Uyku", "Sleep"),
            ["Focus.Profile.DoNotDisturb.Name"] = ("Rahatsız Etmeyin", "Do Not Disturb"),
            ["Focus.Profile.Custom.Name"] = ("Özel Odak", "Custom Focus"),
            ["Focus.Settings.ProfileCount"] = ("{0} / {1} profil", "{0} of {1} profiles"),
            ["Focus.Settings.ProfileSummary"] = ("{0} · {1}", "{0} · {1}"),
            ["Focus.Settings.ProfileSummary.OneAutomation"] = ("{0} · {1} · 1 otomasyon", "{0} · {1} · 1 automation"),
            ["Focus.Settings.ProfileSummary.Automated"] = ("{0} · {1} · {2} otomasyon", "{0} · {1} · {2} automations"),
            ["Focus.Settings.AutomationConflict"] = ("{0} çakışma", "{0} conflicts"),
            ["Focus.Settings.Minutes"] = ("{0} dakika", "{0} minutes"),
            ["Focus.Duration.UntilTurnedOff"] = ("Kapatılana kadar", "Until turned off"),
            ["Focus.Visibility.Global"] = ("Genel ayarı kullan", "Use global setting"),
            ["Focus.Visibility.Always"] = ("Sürekli görünür", "Always visible"),
            ["Focus.Visibility.EventsOnly"] = ("Yalnız olaylarda", "Events only"),
            ["Focus.Visibility.Hidden"] = ("Tamamen gizli", "Hidden"),
            ["Focus.Priority.Low"] = ("Düşük", "Low"),
            ["Focus.Priority.Normal"] = ("Normal", "Normal"),
            ["Focus.Priority.Elevated"] = ("Yükseltilmiş", "Elevated"),
            ["Focus.Priority.High"] = ("Yüksek", "High"),
            ["Focus.Priority.Critical"] = ("Kritik", "Critical"),
            ["Focus.Icon.briefcase"] = ("İş", "Work"),
            ["Focus.Icon.game-controller"] = ("Oyun", "Gaming"),
            ["Focus.Icon.moon"] = ("Ay", "Moon"),
            ["Focus.Icon.do-not-disturb"] = ("Rahatsız etme", "Do not disturb"),
            ["Focus.Icon.star"] = ("Yıldız", "Star"),
            ["Focus.Icon.book"] = ("Kitap", "Book"),
            ["Focus.Icon.fitness"] = ("Spor", "Fitness"),
            ["Focus.Icon.leaf"] = ("Yaprak", "Leaf"),
            ["Focus.Settings.Error.Name"] = ("Ad hatalı", "Invalid name"),
            ["Focus.Settings.Error.Color"] = ("Renk hatalı", "Invalid color"),
            ["Focus.Settings.Error.Duration"] = ("Süre hatalı", "Invalid duration"),
            ["Focus.Settings.Error.Modules"] = ("Modül seçin", "Select a module"),
            ["Focus.Settings.Error.ScheduleDays"] = ("Gün seçin", "Select a day"),
            ["Focus.Settings.Error.AutomationTarget"] = ("Uygulama seçin", "Select an application"),
            ["Focus.Automation.Kind.Foreground"] = ("Ön plan", "Foreground"),
            ["Focus.Automation.Kind.Fullscreen"] = ("Tam ekran", "Fullscreen"),
            ["Focus.Automation.Kind.Running"] = ("Çalışıyor", "Running"),
            ["Focus.Automation.AnyApplication"] = ("Herhangi", "Any"),
            ["Focus.Automation.ProcessUnavailable"] = ("Kullanılamıyor", "Unavailable"),
            ["Focus.Settings.Error.DuplicateName"] = ("Aynı ad", "Duplicate name"),
            ["Focus.Settings.Error.Limit"] = ("Sınır", "Limit")
        });

    private static FocusProfile Custom(string id, string name) =>
        new(
            id,
            FocusProfileKind.Custom,
            name,
            "star",
            "#0EA5E9",
            null,
            new FocusProfileBehavior(
                FocusDockVisibility.UseGlobalSetting,
                Array.Empty<string>(),
                ModuleEventPriority.Low,
                true,
                false,
                false),
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
            Current = SettingsValidator.Normalize(update(Current));
            SettingsChanged?.Invoke(
                this,
                new SettingsChangedEventArgs(previous, Current));
        }

        public void Reset() => Current = MiaDockSettings.Default;

        public Task FlushAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeModule : IIslandModule
    {
        public FakeModule(string id)
        {
            Descriptor = new ModuleDescriptor(
                id,
                id,
                100,
                $"{id}.compact",
                $"{id}.expanded",
                new HashSet<ModuleEventKind>(),
                TimeSpan.FromSeconds(5),
                displayNameKey: $"Module.{id}.Name");
        }

        public ModuleDescriptor Descriptor { get; }
        public ModuleLifecycleState LifecycleState => ModuleLifecycleState.Active;
        public bool IsEnabled { get; set; } = true;
        public ModulePresentation? CurrentPresentation => null;
        public event EventHandler<ModulePresentation?>? PresentationChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<ModuleEvent>? EventOccurred
        {
            add { }
            remove { }
        }

        public bool CanExecuteCommand(string commandId) => false;

        public ValueTask<bool> ExecuteCommandAsync(
            string commandId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        public ValueTask ActivateAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask DeactivateAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class FakeApplicationActivityService(
        ApplicationActivitySnapshot snapshot) : IApplicationActivityService
    {
        public ApplicationActivitySnapshot Current { get; } = snapshot;
        public Exception? LastFailure => null;
        public event EventHandler<ApplicationActivitySnapshot>? ActivityChanged
        {
            add { }
            remove { }
        }
        public void Start() { }
        public void Refresh() { }
        public void Dispose() { }
    }

    private sealed class FakeFocusSettingsLauncher : IFocusSettingsLauncher
    {
        public int OpenCount { get; private set; }

        public Task<bool> OpenWindowsFocusSettingsAsync(
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            return Task.FromResult(true);
        }
    }
}
