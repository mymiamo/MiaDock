using System.Globalization;
using MiaDock.Core.Localization;
using MiaDock.Core.Modules;
using MiaDock.Core.Settings;
using MiaDock.Core.Threading;
using MiaDock.Modules.SystemStatus;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;
using MiaDock.Modules.SystemStatus.Settings;
using MiaDock.Modules.SystemStatus.ViewModels;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class VolumeModuleTests
{
    [TestMethod]
    public async Task Activation_ExposesBrowsableVolumePresentation()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var settings = new FakeVolumeSettings(VolumeModuleOptions.Default);
        using var viewModel = CreateViewModel(service, settings);
        using var module = new VolumeModule(service, viewModel, settings);

        await module.ActivateAsync();

        Assert.IsFalse(module.Descriptor.IsPersistent);
        Assert.AreEqual("volume", module.CurrentPresentation?.ModuleId);
        Assert.AreEqual("Speakers", module.CurrentPresentation?.SecondaryText);
        Assert.AreEqual("40%", module.CurrentPresentation?.ValueText);
    }

    [TestMethod]
    public async Task MasterVolumeChange_RaisesConfiguredCoalescedEvent()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var options = VolumeModuleOptions.Default with
        {
            EventDuration = TimeSpan.FromSeconds(4),
            ShowInFullscreen = false
        };
        var settings = new FakeVolumeSettings(options);
        using var viewModel = CreateViewModel(service, settings);
        using var module = new VolumeModule(service, viewModel, settings);
        await module.ActivateAsync();
        ModuleEvent? raised = null;
        module.EventOccurred += (_, moduleEvent) => raised = moduleEvent;

        service.Publish(CreateSnapshot() with { MasterVolume = 0.75 });

        Assert.IsNotNull(raised);
        Assert.AreEqual(ModuleEventPriority.Low, raised.Priority);
        Assert.AreEqual("volume:master", raised.CoalescingKey);
        Assert.AreEqual("75%", raised.Presentation.ValueText);
        Assert.AreEqual(TimeSpan.FromSeconds(4), raised.DisplayDuration);
        Assert.IsFalse(raised.IsFullscreenEligible);
    }

    [TestMethod]
    public async Task OutputDeviceChange_RaisesStatusEvent()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var settings = new FakeVolumeSettings(VolumeModuleOptions.Default);
        using var viewModel = CreateViewModel(service, settings);
        using var module = new VolumeModule(service, viewModel, settings);
        await module.ActivateAsync();
        ModuleEvent? raised = null;
        module.EventOccurred += (_, moduleEvent) => raised = moduleEvent;

        service.Publish(CreateSnapshot() with
        {
            DefaultOutputDeviceId = "headset",
            DefaultOutputDeviceName = "Headset"
        });

        Assert.IsNotNull(raised);
        Assert.AreEqual(ModuleEventKind.StatusChanged, raised.Kind);
        Assert.AreEqual("volume:output-device", raised.CoalescingKey);
        Assert.AreEqual("Headset", raised.Presentation.SecondaryText);
    }

    [TestMethod]
    public async Task ApplicationVolumeChange_RaisesDedicatedVolumeEvent()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var settings = new FakeVolumeSettings(VolumeModuleOptions.Default);
        using var viewModel = CreateViewModel(service, settings);
        using var module = new VolumeModule(service, viewModel, settings);
        await module.ActivateAsync();
        ModuleEvent? raised = null;
        module.EventOccurred += (_, moduleEvent) => raised = moduleEvent;

        service.Publish(CreateSnapshot() with { ApplicationVolume = 0.25 });

        Assert.IsNotNull(raised);
        Assert.AreEqual("volume:application", raised.CoalescingKey);
        Assert.AreEqual("25%", raised.Presentation.ValueText);
        Assert.AreEqual(ModuleEventPriority.Low, raised.Priority);
    }

    [TestMethod]
    public async Task VolumeCommands_UseSharedSystemActivityService()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var settings = new FakeVolumeSettings(VolumeModuleOptions.Default);
        var launcher = new FakeAudioSettingsLauncher();
        using var viewModel = new VolumeModuleViewModel(service, launcher, settings);
        using var module = new VolumeModule(service, viewModel, settings);
        await module.ActivateAsync();

        Assert.IsTrue(await module.ExecuteCommandAsync("master-mute"));
        Assert.IsTrue(await module.ExecuteCommandAsync("open-sound-settings"));
        Assert.AreEqual(1, service.ToggleMuteCount);
        Assert.AreEqual(1, launcher.OpenCount);
    }

    [TestMethod]
    public void Options_ClampDurationAndDefaultMalformedDeviceSetting()
    {
        var envelope = ModuleSettingsEnvelope.VolumeDefault with
        {
            EventDurationSeconds = 90,
            Options = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["showOutputDeviceName"] =
                    System.Text.Json.JsonSerializer.SerializeToElement("invalid")
            }
        };

        var options = VolumeModuleOptions.FromEnvelope(envelope);

        Assert.AreEqual(TimeSpan.FromSeconds(10), options.EventDuration);
        Assert.IsTrue(options.ShowOutputDeviceName);
    }

    [TestMethod]
    public async Task MixerViewModel_ReconcilesSessionsAndRoutesControls()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var settings = new FakeVolumeSettings(VolumeModuleOptions.Default);
        var mixer = new FakeAudioMixerService();
        using var viewModel = new VolumeModuleViewModel(
            service,
            new FakeAudioSettingsLauncher(),
            settings,
            mixer: mixer);
        var session = new AudioMixerSessionSnapshot(
            "process:42",
            42,
            string.Empty,
            "spotify",
            null,
            0.65,
            false,
            true,
            false,
            0.8);

        mixer.Publish(new AudioMixerSnapshot(
            SystemActivityServiceState.Ready,
            "speakers",
            "Speakers",
            [session],
            true));

        Assert.IsTrue(viewModel.HasMixerSessions);
        Assert.HasCount(1, viewModel.MixerSessions);
        Assert.AreEqual("Spotify", viewModel.MixerSessions[0].DisplayName);
        Assert.AreEqual("65%", viewModel.MixerSessions[0].VolumeText);

        viewModel.SetMixerActive(true);
        viewModel.SetMixerActive(true);
        await viewModel.MixerSessions[0].SetVolumeAsync(30);
        await viewModel.MixerSessions[0].ToggleMuteCommand.ExecuteAsync(null);
        viewModel.SetMixerActive(false);
        viewModel.SetMixerActive(false);

        CollectionAssert.AreEqual(new[] { true, false }, mixer.MeteringStates);
        Assert.AreEqual(("process:42", 0.3), mixer.LastVolume);
        Assert.AreEqual("process:42", mixer.LastMutedSession);
    }

    [TestMethod]
    public void MixerViewModel_RemovesExpiredSessionsWithoutRecreatingExistingItem()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var settings = new FakeVolumeSettings(VolumeModuleOptions.Default);
        var mixer = new FakeAudioMixerService();
        using var viewModel = new VolumeModuleViewModel(
            service,
            new FakeAudioSettingsLauncher(),
            settings,
            mixer: mixer);
        var first = new AudioMixerSessionSnapshot(
            "process:1", 1, "Player", "player", null, 0.5, false, true, false, 0);
        var second = new AudioMixerSessionSnapshot(
            "process:2", 2, "Browser", "browser", null, 0.4, false, true, false, 0);
        mixer.Publish(AudioMixerSnapshot.Default with
        {
            ServiceState = SystemActivityServiceState.Ready,
            Sessions = [first, second]
        });
        var retained = viewModel.MixerSessions[0];

        mixer.Publish(AudioMixerSnapshot.Default with
        {
            ServiceState = SystemActivityServiceState.Ready,
            Sessions = [first with { Volume = 0.7 }]
        });

        Assert.HasCount(1, viewModel.MixerSessions);
        Assert.AreSame(retained, viewModel.MixerSessions[0]);
        Assert.AreEqual("70%", retained.VolumeText);
    }

    [TestMethod]
    public void MixerViewModel_DisposalStopsActiveMeteringExactlyOnce()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var settings = new FakeVolumeSettings(VolumeModuleOptions.Default);
        var mixer = new FakeAudioMixerService();
        var viewModel = new VolumeModuleViewModel(
            service,
            new FakeAudioSettingsLauncher(),
            settings,
            mixer: mixer);

        viewModel.SetMixerActive(true);
        viewModel.Dispose();
        viewModel.Dispose();
        viewModel.SetMixerActive(true);
        mixer.Publish(AudioMixerSnapshot.Default with
        {
            ServiceState = SystemActivityServiceState.Ready,
            Sessions =
            [
                new AudioMixerSessionSnapshot(
                    "process:after-dispose", 2, "Ignored", "ignored", null,
                    0.5, false, true, false, 0)
            ]
        });

        CollectionAssert.AreEqual(new[] { true, false }, mixer.MeteringStates);
        Assert.IsFalse(viewModel.HasMixerSessions);
    }

    [TestMethod]
    public async Task MixerViewModel_UnsupportedSessionRejectsDirectControlCommands()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var settings = new FakeVolumeSettings(VolumeModuleOptions.Default);
        var mixer = new FakeAudioMixerService();
        using var viewModel = new VolumeModuleViewModel(
            service,
            new FakeAudioSettingsLauncher(),
            settings,
            mixer: mixer);
        mixer.Publish(AudioMixerSnapshot.Default with
        {
            ServiceState = SystemActivityServiceState.Ready,
            Sessions =
            [
                new AudioMixerSessionSnapshot(
                    "exclusive:1", 1, "Exclusive player", "player", null,
                    0.8, false, false, false, 0.4)
            ]
        });

        var session = viewModel.MixerSessions.Single();

        Assert.IsFalse(session.ToggleMuteCommand.CanExecute(null));
        Assert.IsFalse(await session.SetVolumeAsync(20));
        Assert.IsNull(mixer.LastVolume);
        Assert.IsNull(mixer.LastMutedSession);
    }

    [TestMethod]
    public void MixerViewModel_TenThousandRapidUpdatesKeepLatestStableSession()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var settings = new FakeVolumeSettings(VolumeModuleOptions.Default);
        var mixer = new FakeAudioMixerService();
        using var viewModel = new VolumeModuleViewModel(
            service,
            new FakeAudioSettingsLauncher(),
            settings,
            mixer: mixer);
        var session = new AudioMixerSessionSnapshot(
            "process:42", 42, "Player", "player", null,
            0, false, true, false, 0);
        mixer.Publish(AudioMixerSnapshot.Default with
        {
            ServiceState = SystemActivityServiceState.Ready,
            Sessions = [session]
        });
        var retained = viewModel.MixerSessions.Single();

        for (var update = 1; update <= 10_000; update++)
        {
            mixer.Publish(AudioMixerSnapshot.Default with
            {
                ServiceState = SystemActivityServiceState.Ready,
                Sessions =
                [
                    session with
                    {
                        Volume = update / 10_000d,
                        PeakLevel = (update % 100) / 100d
                    }
                ]
            });
        }

        Assert.HasCount(1, viewModel.MixerSessions);
        Assert.AreSame(retained, viewModel.MixerSessions.Single());
        Assert.AreEqual("100%", retained.VolumeText);
        Assert.AreEqual(0, retained.Snapshot.PeakLevel, 0.0001);
        Assert.AreEqual(0, retained.PeakLevel, 0.0001);
    }

    [TestMethod]
    public void MixerViewModel_DuplicateOrBlankSessionKeysAreCoalescedSafely()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var settings = new FakeVolumeSettings(VolumeModuleOptions.Default);
        var mixer = new FakeAudioMixerService();
        using var viewModel = new VolumeModuleViewModel(
            service,
            new FakeAudioSettingsLauncher(),
            settings,
            mixer: mixer);

        mixer.Publish(AudioMixerSnapshot.Default with
        {
            ServiceState = SystemActivityServiceState.Ready,
            Sessions =
            [
                new AudioMixerSessionSnapshot(
                    "process:1", 1, "Old", "player", null,
                    0.2, false, true, false, 0),
                new AudioMixerSessionSnapshot(
                    string.Empty, 2, "Invalid", "invalid", null,
                    0.5, false, true, false, 0),
                new AudioMixerSessionSnapshot(
                    "process:1", 1, "Latest", "player", null,
                    0.9, false, true, false, 0)
            ]
        });

        Assert.HasCount(1, viewModel.MixerSessions);
        Assert.AreEqual("Latest", viewModel.MixerSessions.Single().DisplayName);
        Assert.AreEqual("90%", viewModel.MixerSessions.Single().VolumeText);
    }

    [TestMethod]
    public void MixerAccessibilityLabels_UpdateImmediatelyWithLanguageAndMuteState()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var settings = new FakeVolumeSettings(VolumeModuleOptions.Default);
        var mixer = new FakeAudioMixerService();
        var localization = new FakeLocalizationService();
        using var viewModel = new VolumeModuleViewModel(
            service,
            new FakeAudioSettingsLauncher(),
            settings,
            localization,
            mixer);
        var snapshot = new AudioMixerSessionSnapshot(
            "process:1", 1, "Player", "player", null,
            0.5, false, true, false, 0);
        mixer.Publish(AudioMixerSnapshot.Default with
        {
            ServiceState = SystemActivityServiceState.Ready,
            Sessions = [snapshot]
        });
        var session = viewModel.MixerSessions.Single();

        Assert.AreEqual("Player ses seviyesi", session.VolumeAutomationName);
        Assert.AreEqual("Player sesini kapat", session.MuteAutomationName);

        localization.SetLanguage(AppLanguage.English);
        Assert.AreEqual("Player volume level", session.VolumeAutomationName);
        Assert.AreEqual("Mute Player", session.MuteAutomationName);

        mixer.Publish(AudioMixerSnapshot.Default with
        {
            ServiceState = SystemActivityServiceState.Ready,
            Sessions = [snapshot with { IsMuted = true }]
        });
        Assert.AreEqual("Unmute Player", session.MuteAutomationName);
    }

    [TestMethod]
    public async Task BackgroundAudioEvents_AreMarshaledBeforeUpdatingUiCollections()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var settings = new FakeVolumeSettings(VolumeModuleOptions.Default);
        var mixer = new FakeAudioMixerService();
        var dispatcher = new QueuedUiDispatcher();
        using var viewModel = new VolumeModuleViewModel(
            service,
            new FakeAudioSettingsLauncher(),
            settings,
            mixer: mixer,
            uiDispatcher: dispatcher);
        var snapshot = AudioMixerSnapshot.Default with
        {
            ServiceState = SystemActivityServiceState.Ready,
            Sessions =
            [
                new AudioMixerSessionSnapshot(
                    "process:1", 1, "Player", "player", null,
                    0.5, false, true, false, 0)
            ]
        };

        await Task.Run(() => mixer.Publish(snapshot));

        Assert.IsEmpty(viewModel.MixerSessions);
        Assert.AreEqual(1, dispatcher.PendingCount);
        dispatcher.Drain();
        Assert.HasCount(1, viewModel.MixerSessions);
        Assert.AreEqual("Player", viewModel.MixerSessions.Single().DisplayName);
    }

    private static VolumeModuleViewModel CreateViewModel(
        FakeSystemActivityService service,
        FakeVolumeSettings settings) =>
        new(service, new FakeAudioSettingsLauncher(), settings);

    private static SystemActivitySnapshot CreateSnapshot() => new(
        SystemActivityServiceState.Ready,
        true,
        0.4,
        false,
        ApplicationVolumeAvailability.Available,
        0.6,
        false,
        MicrophoneUsageState.Idle,
        CameraDeviceAvailability.Available,
        CameraAccessState.Allowed,
        CallActivityState.None,
        "speakers",
        "Speakers");

    private sealed class FakeSystemActivityService(SystemActivitySnapshot current)
        : ISystemActivityService
    {
        public SystemActivitySnapshot Current { get; private set; } = current;
        public int ToggleMuteCount { get; private set; }

        public event EventHandler<SystemActivitySnapshot>? SnapshotChanged;

        public Task StartAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> SetMasterVolumeAsync(
            double volume,
            CancellationToken cancellationToken = default)
        {
            Publish(Current with { MasterVolume = Math.Clamp(volume, 0, 1) });
            return Task.FromResult(true);
        }

        public Task<bool> ToggleMasterMuteAsync(
            CancellationToken cancellationToken = default)
        {
            ToggleMuteCount++;
            Publish(Current with { IsMasterMuted = !Current.IsMasterMuted });
            return Task.FromResult(true);
        }

        public Task<bool> SetApplicationVolumeAsync(
            double volume,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> ToggleApplicationMuteAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Publish(SystemActivitySnapshot snapshot)
        {
            Current = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        }
    }

    private sealed class FakeVolumeSettings(VolumeModuleOptions current)
        : IVolumeModuleSettings
    {
        public VolumeModuleOptions Current { get; private set; } = current;

        public event EventHandler<VolumeModuleOptions>? Changed;

        public void Publish(VolumeModuleOptions options)
        {
            Current = options;
            Changed?.Invoke(this, options);
        }
    }

    private sealed class FakeAudioSettingsLauncher : IAudioSettingsLauncher
    {
        public int OpenCount { get; private set; }

        public Task<bool> OpenSoundSettingsAsync(
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeAudioMixerService : IAudioMixerService
    {
        public AudioMixerSnapshot CurrentMixer { get; private set; } =
            AudioMixerSnapshot.Default;
        public List<bool> MeteringStates { get; } = [];
        public (string SessionKey, double Volume)? LastVolume { get; private set; }
        public string? LastMutedSession { get; private set; }

        public event EventHandler<AudioMixerSnapshot>? MixerChanged;

        public Task StartAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void SetMeteringEnabled(bool enabled) =>
            MeteringStates.Add(enabled);

        public Task<bool> SetSessionVolumeAsync(
            string sessionKey,
            double volume,
            CancellationToken cancellationToken = default)
        {
            LastVolume = (sessionKey, volume);
            return Task.FromResult(true);
        }

        public Task<bool> ToggleSessionMuteAsync(
            string sessionKey,
            CancellationToken cancellationToken = default)
        {
            LastMutedSession = sessionKey;
            return Task.FromResult(true);
        }

        public void Publish(AudioMixerSnapshot snapshot)
        {
            CurrentMixer = snapshot;
            MixerChanged?.Invoke(this, snapshot);
        }
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Turkish;

        public CultureInfo CurrentCulture =>
            CurrentLanguage == AppLanguage.English
                ? CultureInfo.GetCultureInfo("en-US")
                : CultureInfo.GetCultureInfo("tr-TR");

        public event EventHandler? LanguageChanged;

        public void SetLanguage(AppLanguage language)
        {
            if (CurrentLanguage == language)
            {
                return;
            }

            CurrentLanguage = language;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        public string Get(string key, params object?[] arguments)
        {
            var format = (key, CurrentLanguage) switch
            {
                ("Mixer.SessionVolumeAutomation", AppLanguage.English) =>
                    "{0} volume level",
                ("Mixer.SessionVolumeAutomation", _) =>
                    "{0} ses seviyesi",
                ("Mixer.SessionMuteAutomation", AppLanguage.English) =>
                    "Mute {0}",
                ("Mixer.SessionMuteAutomation", _) =>
                    "{0} sesini kapat",
                ("Mixer.SessionUnmuteAutomation", AppLanguage.English) =>
                    "Unmute {0}",
                ("Mixer.SessionUnmuteAutomation", _) =>
                    "{0} sesini aç",
                _ => key
            };
            return string.Format(CurrentCulture, format, arguments);
        }
    }

    private sealed class QueuedUiDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _callbacks = new();

        public bool HasThreadAccess => false;
        public int PendingCount => _callbacks.Count;

        public bool TryEnqueue(Action callback)
        {
            _callbacks.Enqueue(callback);
            return true;
        }

        public void Drain()
        {
            while (_callbacks.TryDequeue(out var callback))
            {
                callback();
            }
        }
    }
}
