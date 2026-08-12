using MiaDock.App.Services;
using MiaDock.Core.Input;
using MiaDock.Core.Localization;
using MiaDock.Core.Modules;
using MiaDock.Core.Threading;

namespace MiaDock.App.Modules;

public sealed class KeyboardLockModule : IIslandModule, IAsyncDisposable
{
    public const string ModuleId = "keyboard-locks";
    private static readonly TimeSpan NotificationDuration = TimeSpan.FromSeconds(2);

    private readonly IKeyboardLockMonitor _monitor;
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _localization;
    private readonly IUiDispatcher _dispatcher;
    private bool _isEnabled = true;

    public KeyboardLockModule(
        IKeyboardLockMonitor monitor,
        ISettingsService settings,
        ILocalizationService localization,
        IUiDispatcher dispatcher)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _monitor.StateChanged += OnStateChanged;
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleId,
        "Klavye kilitleri",
        180,
        "GenericCompactModuleView",
        "GenericExpandedModuleView",
        new HashSet<ModuleEventKind> { ModuleEventKind.StatusChanged },
        NotificationDuration,
        notificationViewKey: null,
        persistentPriority: 0,
        isPersistent: false,
        iconGlyph: "\uE765",
        displayNameKey: "KeyboardLock.ModuleName");

    public ModuleLifecycleState LifecycleState { get; private set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            PresentationChanged?.Invoke(this, CurrentPresentation);
        }
    }

    public ModulePresentation? CurrentPresentation => null;

    public event EventHandler<ModulePresentation?>? PresentationChanged;

    public event EventHandler<ModuleEvent>? EventOccurred;

    public bool CanExecuteCommand(string commandId) => false;

    public ValueTask<bool> ExecuteCommandAsync(
        string commandId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    public async ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Active;
        if (ShouldMonitor)
        {
            await _monitor.StartAsync(cancellationToken);
        }

        PresentationChanged?.Invoke(this, null);
    }

    public async ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Inactive;
        await _monitor.StopAsync(cancellationToken);
        PresentationChanged?.Invoke(this, null);
    }

    public async ValueTask DisposeAsync()
    {
        _monitor.StateChanged -= OnStateChanged;
        _settings.SettingsChanged -= OnSettingsChanged;
        await _monitor.DisposeAsync();
    }

    private bool ShouldMonitor =>
        IsEnabled &&
        LifecycleState == ModuleLifecycleState.Active &&
        _settings.Current.General.ShowKeyboardLockEvents;

    private async void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        try
        {
            if (ShouldMonitor)
            {
                await _monitor.StartAsync();
            }
            else
            {
                await _monitor.StopAsync();
            }
        }
        catch
        {
            // Monitor start/stop failures should not crash settings propagation.
        }
    }

    private void OnStateChanged(object? sender, KeyboardLockStateChangedEventArgs args)
    {
        if (!ShouldMonitor)
        {
            return;
        }

        void Publish()
        {
            if (!ShouldMonitor)
            {
                return;
            }

            var name = args.Kind switch
            {
                KeyboardLockKind.CapsLock => _localization.Get("KeyboardLock.CapsLock"),
                KeyboardLockKind.NumLock => _localization.Get("KeyboardLock.NumLock"),
                KeyboardLockKind.ScrollLock => _localization.Get("KeyboardLock.ScrollLock"),
                _ => args.Kind.ToString()
            };
            var stateText = args.IsOn
                ? _localization.Get("KeyboardLock.On")
                : _localization.Get("KeyboardLock.Off");
            var primary = _localization.Get(
                args.IsOn ? "KeyboardLock.OnFormat" : "KeyboardLock.OffFormat",
                name);
            var glyph = args.Kind switch
            {
                KeyboardLockKind.CapsLock => "\uE8D2",
                KeyboardLockKind.NumLock => "\uE8EF",
                _ => "\uE7C4"
            };

            var presentation = new ModulePresentation(
                ModuleId,
                primary,
                name,
                glyph,
                ModuleIndicatorKind.StatusDot,
                valueText: stateText,
                presentationKind: ModulePresentationKind.Status);
            EventOccurred?.Invoke(this, new ModuleEvent(
                ModuleId,
                ModuleEventKind.StatusChanged,
                presentation,
                NotificationDuration,
                args.OccurredAtUtc,
                ModuleEventPriority.Normal,
                $"keyboard-lock:{args.Kind}",
                isFullscreenEligible: false));
        }

        if (_dispatcher.HasThreadAccess)
        {
            Publish();
        }
        else
        {
            _dispatcher.TryEnqueue(Publish);
        }
    }
}
