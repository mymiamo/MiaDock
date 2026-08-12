using MiaDock.App.Modules;
using MiaDock.Core.Logging;
using MiaDock.Core.Modules;
using MiaDock.Core.Settings;

namespace MiaDock.App.Services;

public sealed class ModuleSettingsCoordinator : IDisposable
{
    private readonly ISettingsService _settings;
    private readonly IIslandModuleRegistry _registry;
    private readonly ILogService _log;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _disposeCancellation = new();
    private MiaDockSettings? _pendingSettings;
    private bool _applyLoopRunning;
    private bool _started;
    private bool _disposed;

    public ModuleSettingsCoordinator(
        ISettingsService settings,
        IIslandModuleRegistry registry,
        ILogService log)
    {
        _settings = settings;
        _registry = registry;
        _log = log;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            if (_started) return;
            _settings.SettingsChanged += OnSettingsChanged;
            _started = true;
        }
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        lock (_gate)
        {
            if (!_started || _disposed)
            {
                return;
            }

            _pendingSettings = args.Current;
            if (_applyLoopRunning)
            {
                return;
            }

            _applyLoopRunning = true;
        }

        _ = ApplyLatestSettingsAsync();
    }

    private async Task ApplyLatestSettingsAsync()
    {
        var cancellationToken = _disposeCancellation.Token;
        try
        {
            while (true)
            {
                MiaDockSettings settings;
                lock (_gate)
                {
                    if (!_started || _disposed || _pendingSettings is null)
                    {
                        _applyLoopRunning = false;
                        return;
                    }

                    settings = _pendingSettings;
                    _pendingSettings = null;
                }

                foreach (var module in _registry.Modules)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var enabled = module.Descriptor.Id switch
                    {
                        "store-update" => true,
                        KeyboardLockModule.ModuleId => settings.General.ShowKeyboardLockEvents,
                        UsbDeviceModule.ModuleId => settings.General.ShowUsbDeviceEvents,
                        _ => settings.Modules.TryGetValue(
                                 module.Descriptor.Id,
                                 out var moduleSettings) &&
                             moduleSettings.IsEnabled
                    };
                    if (module.IsEnabled == enabled)
                    {
                        continue;
                    }

                    try
                    {
                        await _registry.SetEnabledAsync(
                            module.Descriptor.Id,
                            enabled,
                            cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        _log.Write(
                            TechnicalLogLevel.Warning,
                            TechnicalEventIds.ModuleSettingsApplyFailed,
                            "Modules",
                            "A module setting could not be applied; remaining modules will continue.",
                            exception,
                            new Dictionary<string, object?>
                            {
                                ["moduleId"] = module.Descriptor.Id,
                                ["enabled"] = enabled
                            });
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_gate)
            {
                _applyLoopRunning = false;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (_started)
            {
                _settings.SettingsChanged -= OnSettingsChanged;
            }

            _started = false;
            _disposed = true;
            _pendingSettings = null;
        }

        _disposeCancellation.Cancel();
        _disposeCancellation.Dispose();
    }
}
