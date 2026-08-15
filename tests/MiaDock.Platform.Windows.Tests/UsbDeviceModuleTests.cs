using System.Globalization;
using MiaDock.App.Modules;
using MiaDock.App.Services;
using MiaDock.Core.Input;
using MiaDock.Core.Localization;
using MiaDock.Core.Settings;
using MiaDock.Core.Threading;
using MiaDock.Core.Modules;

namespace MiaDock.Platform.Windows.Tests;

[TestClass]
public sealed class UsbDeviceModuleTests
{
    [TestMethod]
    public async Task DisabledUsbEvents_DoNotStartMonitoringOrPublishFeedback()
    {
        var settings = new FakeSettingsService
        {
            Current = MiaDockSettings.Default with
            {
                General = MiaDockSettings.Default.General with { ShowUsbDeviceEvents = false }
            }
        };
        var monitor = new FakeMonitor();
        var module = new UsbDeviceModule(monitor, settings, new FakeLocalization(), new ImmediateDispatcher());
        var eventCount = 0;
        module.EventOccurred += (_, _) => eventCount++;

        await module.ActivateAsync();
        monitor.Raise(new UsbDeviceChangedEventArgs(true, string.Empty, "USB device", DateTimeOffset.UtcNow, "device"));

        Assert.AreEqual(0, monitor.StartCount);
        Assert.AreEqual(0, eventCount);

        await module.DisposeAsync();
    }

    [TestMethod]
    public async Task LegacyUsbEvents_UseDeviceCuesWhenDeviceHubIsDisabled()
    {
        var modules = MiaDockSettings.Default.Modules.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        modules["device-hub"] = modules["device-hub"] with { IsEnabled = false };
        var settings = new FakeSettingsService
        {
            Current = MiaDockSettings.Default with { Modules = modules }
        };
        var monitor = new FakeMonitor();
        var module = new UsbDeviceModule(monitor, settings, new FakeLocalization(), new ImmediateDispatcher());
        var events = new List<ModuleEvent>();
        module.EventOccurred += (_, value) => events.Add(value);

        await module.ActivateAsync();
        monitor.Raise(new UsbDeviceChangedEventArgs(true, "E:\\", "USB drive", DateTimeOffset.UtcNow, "device"));
        monitor.Raise(new UsbDeviceChangedEventArgs(false, "E:\\", "USB drive", DateTimeOffset.UtcNow, "device"));

        Assert.HasCount(2, events);
        Assert.AreEqual(AudibleNotificationCue.DeviceConnected, events[0].AudibleCue);
        Assert.AreEqual(AudibleNotificationCue.DeviceDisconnected, events[1].AudibleCue);

        await module.DisposeAsync();
    }

    private sealed class FakeMonitor : IUsbDeviceMonitor
    {
        public event EventHandler<UsbDeviceChangedEventArgs>? DeviceChanged;
        public bool IsRunning { get; private set; }
        public int StartCount { get; private set; }
        public ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            IsRunning = true;
            return ValueTask.FromResult<IAsyncDisposable>(new MonitorLease(this));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Raise(UsbDeviceChangedEventArgs args) => DeviceChanged?.Invoke(this, args);

        private sealed class MonitorLease(FakeMonitor owner) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                owner.IsRunning = false;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public MiaDockSettings Current { get; set; } = MiaDockSettings.Default;
        public Exception? LastSaveFailure => null;
        public string SettingsFilePath => string.Empty;
        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged { add { } remove { } }
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Func<MiaDockSettings, MiaDockSettings> update) => Current = update(Current);
        public void Reset() => Current = MiaDockSettings.Default;
        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeLocalization : ILocalizationService
    {
        public AppLanguage CurrentLanguage => AppLanguage.Turkish;
        public CultureInfo CurrentCulture => CultureInfo.GetCultureInfo("tr-TR");
        public event EventHandler? LanguageChanged { add { } remove { } }
        public void SetLanguage(AppLanguage language) { }
        public string Get(string key, params object?[] arguments) => key;
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

}
