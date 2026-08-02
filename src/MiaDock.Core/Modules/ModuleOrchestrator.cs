using MiaDock.Core.Focus;

namespace MiaDock.Core.Modules;

public sealed class ModuleOrchestrator : IModuleOrchestrator
{
    public const int MaximumPendingEvents = 32;
    public static readonly TimeSpan DefaultTemporarySelectionDuration = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan MinimumTemporarySelectionDuration = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan MaximumTemporarySelectionDuration = TimeSpan.FromSeconds(30);

    private readonly IIslandModuleRegistry _registry;
    private readonly TimeProvider _timeProvider;
    private readonly IFocusPolicyService? _focusPolicy;
    private readonly object _gate = new();
    private readonly List<ModuleEvent> _pendingEvents = [];
    private ModuleEvent? _activeEvent;
    private string? _manuallySelectedModuleId;
    private DateTimeOffset? _temporarySelectionExpiresAtUtc;
    private TimeSpan _temporarySelectionDuration;
    private bool _isTemporarySelectionExpirationSuspended;
    private bool _isDefaultManuallySelected;
    private bool _disposed;
    private long _receivedEvents;
    private long _coalescedEvents;
    private long _droppedEvents;
    private long _expiredEvents;

    public ModuleOrchestrator(
        IIslandModuleRegistry registry,
        TimeProvider? timeProvider = null,
        TimeSpan? temporarySelectionDuration = null,
        IFocusPolicyService? focusPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _focusPolicy = focusPolicy;
        _temporarySelectionDuration = temporarySelectionDuration ?? DefaultTemporarySelectionDuration;
        ValidateTemporarySelectionDuration(_temporarySelectionDuration);
        _registry.ActivePresentationChanged += OnActivePresentationChanged;
        _registry.ModuleEventOccurred += OnModuleEventOccurred;
        if (_focusPolicy is not null)
        {
            _focusPolicy.PolicyChanged += OnFocusPolicyChanged;
        }
    }

    public IReadOnlyList<ModuleDisplayState> AvailableModules
    {
        get
        {
            lock (_gate)
            {
                return GetAvailableModules().ToArray();
            }
        }
    }

    public ModuleDisplayState? CurrentDisplay
    {
        get
        {
            lock (_gate)
            {
                return ResolveCurrentDisplay();
            }
        }
    }

    public ModuleEvent? ActiveEvent
    {
        get
        {
            lock (_gate)
            {
                RemoveExpiredEvents();
                return _activeEvent;
            }
        }
    }

    public int PendingEventCount
    {
        get
        {
            lock (_gate)
            {
                RemoveExpiredEvents();
                return _pendingEvents.Count;
            }
        }
    }

    public TimeSpan TemporarySelectionDuration
    {
        get
        {
            lock (_gate)
            {
                return _temporarySelectionDuration;
            }
        }
    }

    public DateTimeOffset? TemporarySelectionExpiresAtUtc
    {
        get
        {
            lock (_gate)
            {
                return _temporarySelectionExpiresAtUtc;
            }
        }
    }

    public ModuleOrchestratorStatistics Statistics
    {
        get
        {
            lock (_gate)
            {
                RemoveExpiredEvents();
                return new(
                    _receivedEvents,
                    _coalescedEvents,
                    _droppedEvents,
                    _expiredEvents,
                    _pendingEvents.Count);
            }
        }
    }

    public event EventHandler<ModuleDisplayState?>? CurrentDisplayChanged;

    public event EventHandler<ModuleEvent?>? ActiveEventChanged;

    public bool SelectModule(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ModuleDisplayState? display;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (GetAvailableModules().All(item => item.Descriptor.Id != moduleId))
            {
                return false;
            }

            _manuallySelectedModuleId = moduleId;
            _isDefaultManuallySelected = false;
            RefreshTemporarySelectionExpiration(reset: true);
            display = ResolveCurrentDisplay();
        }

        CurrentDisplayChanged?.Invoke(this, display);
        return true;
    }

    public bool SelectDefault()
    {
        ModuleDisplayState? display;
        lock (_gate)
        {
            ThrowIfDisposed();
            _manuallySelectedModuleId = null;
            _isDefaultManuallySelected = true;
            CancelTemporarySelectionExpiration();
            display = ResolveCurrentDisplay();
        }

        CurrentDisplayChanged?.Invoke(this, display);
        return true;
    }

    public bool MoveSelection(int offset)
    {
        if (offset == 0)
        {
            return false;
        }

        ModuleDisplayState? display;
        lock (_gate)
        {
            ThrowIfDisposed();
            var modules = GetAvailableModules();
            if (modules.Count == 0)
            {
                _manuallySelectedModuleId = null;
                _isDefaultManuallySelected = true;
                CancelTemporarySelectionExpiration();
                display = null;
            }
            else
            {
                var currentId = _manuallySelectedModuleId ?? ResolvePersistentDisplay()?.Descriptor.Id;
                var moduleIndex = modules.FindIndex(item => item.Descriptor.Id == currentId);
                var currentIndex = _isDefaultManuallySelected ? 0 : moduleIndex >= 0 ? moduleIndex + 1 : 0;
                var direction = Math.Sign(offset);
                var itemCount = modules.Count + 1;
                var nextIndex = (currentIndex + direction + itemCount) % itemCount;
                _isDefaultManuallySelected = nextIndex == 0;
                _manuallySelectedModuleId = nextIndex == 0 ? null : modules[nextIndex - 1].Descriptor.Id;
                RefreshTemporarySelectionExpiration(reset: true);
                display = ResolveCurrentDisplay();
            }
        }

        CurrentDisplayChanged?.Invoke(this, display);
        return true;
    }

    public void EndManualSelection()
    {
        ModuleDisplayState? display;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_manuallySelectedModuleId is null && !_isDefaultManuallySelected)
            {
                return;
            }

            _manuallySelectedModuleId = null;
            _isDefaultManuallySelected = false;
            CancelTemporarySelectionExpiration();
            display = ResolveCurrentDisplay();
        }

        CurrentDisplayChanged?.Invoke(this, display);
    }

    public bool NotifyUserInteraction()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (GetManuallySelectedDisplay() is not { KeepsManualSelection: false })
            {
                return false;
            }

            RefreshTemporarySelectionExpiration(reset: true);
            return true;
        }
    }

    public void UpdateTemporarySelectionDuration(TimeSpan duration)
    {
        ValidateTemporarySelectionDuration(duration);
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_temporarySelectionDuration == duration)
            {
                return;
            }

            _temporarySelectionDuration = duration;
            RefreshTemporarySelectionExpiration(reset: true);
        }
    }

    public bool ExpireTemporarySelection()
    {
        ModuleDisplayState? display;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (!ExpireTemporarySelectionIfDue())
            {
                return false;
            }

            display = ResolveCurrentDisplay();
        }

        CurrentDisplayChanged?.Invoke(this, display);
        return true;
    }

    public bool CompleteActiveEvent()
    {
        ModuleEvent? active;
        ModuleDisplayState? display;
        lock (_gate)
        {
            ThrowIfDisposed();
            _activeEvent = null;
            RemoveExpiredEvents();
            active = _activeEvent;
            display = ResolveCurrentDisplay();
        }

        ActiveEventChanged?.Invoke(this, active);
        CurrentDisplayChanged?.Invoke(this, display);
        return active is not null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _temporarySelectionExpiresAtUtc = null;
        }

        _registry.ActivePresentationChanged -= OnActivePresentationChanged;
        _registry.ModuleEventOccurred -= OnModuleEventOccurred;
        if (_focusPolicy is not null)
        {
            _focusPolicy.PolicyChanged -= OnFocusPolicyChanged;
        }
    }

    private void OnFocusPolicyChanged(object? sender, EventArgs args)
    {
        ModuleEvent? active;
        ModuleDisplayState? display;
        bool activeChanged;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var previousActive = _activeEvent;
            RemoveDisallowedEvents();
            if (_manuallySelectedModuleId is { } selectedId &&
                !AllowsModule(selectedId))
            {
                _manuallySelectedModuleId = null;
                _isDefaultManuallySelected = true;
            }

            RefreshTemporarySelectionExpiration(reset: false);
            active = _activeEvent;
            display = ResolveCurrentDisplay();
            activeChanged = !ReferenceEquals(previousActive, active);
        }

        if (activeChanged)
        {
            ActiveEventChanged?.Invoke(this, active);
        }

        CurrentDisplayChanged?.Invoke(this, display);
    }

    private void OnActivePresentationChanged(object? sender, ModulePresentation? presentation)
    {
        ModuleDisplayState? display;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_manuallySelectedModuleId is not null &&
                GetAvailableModules().All(item => item.Descriptor.Id != _manuallySelectedModuleId))
            {
                _manuallySelectedModuleId = null;
                _isDefaultManuallySelected = true;
            }

            RefreshTemporarySelectionExpiration(reset: false);
            display = ResolveCurrentDisplay();
        }

        CurrentDisplayChanged?.Invoke(this, display);
    }

    private void OnModuleEventOccurred(object? sender, ModuleEvent moduleEvent)
    {
        ModuleEvent? active;
        ModuleDisplayState? display;
        bool activeChanged;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _receivedEvents++;
            if (!AllowsEvent(moduleEvent))
            {
                return;
            }

            if (moduleEvent.ExpiresAtUtc <= GetUtcNow())
            {
                _expiredEvents++;
                return;
            }
            if (FindModule(moduleEvent.ModuleId) is null) return;

            RemoveExpiredEvents();
            var previousActive = _activeEvent;
            Coalesce(moduleEvent);
            active = _activeEvent;
            display = ResolveCurrentDisplay();
            activeChanged = !ReferenceEquals(previousActive, active);
        }

        if (activeChanged)
        {
            ActiveEventChanged?.Invoke(this, active);
            CurrentDisplayChanged?.Invoke(this, display);
        }
    }

    private void Coalesce(ModuleEvent incoming)
    {
        if (_activeEvent?.CoalescingKey == incoming.CoalescingKey)
        {
            _coalescedEvents++;
            _activeEvent = incoming;
            return;
        }

        var pendingIndex = _pendingEvents.FindIndex(item => item.CoalescingKey == incoming.CoalescingKey);
        if (pendingIndex >= 0)
        {
            _coalescedEvents++;
            _pendingEvents.RemoveAt(pendingIndex);
            if (_activeEvent is not null && incoming.Priority > _activeEvent.Priority)
            {
                Enqueue(_activeEvent);
                _activeEvent = incoming;
            }
            else
            {
                _pendingEvents.Insert(Math.Min(pendingIndex, _pendingEvents.Count), incoming);
            }

            return;
        }

        if (_activeEvent is null)
        {
            _activeEvent = incoming;
            return;
        }

        if (incoming.Priority > _activeEvent.Priority)
        {
            Enqueue(_activeEvent);
            _activeEvent = incoming;
            return;
        }

        Enqueue(incoming);
    }

    private void Enqueue(ModuleEvent moduleEvent)
    {
        _pendingEvents.Add(moduleEvent);
        if (_pendingEvents.Count <= MaximumPendingEvents)
        {
            return;
        }

        var itemToDrop = _pendingEvents
            .Select((item, index) => (item, index))
            .OrderBy(pair => pair.item.Priority)
            .ThenBy(pair => pair.index)
            .First();
        _pendingEvents.RemoveAt(itemToDrop.index);
        _droppedEvents++;
    }

    private void PromoteNextEvent()
    {
        if (_pendingEvents.Count == 0)
        {
            return;
        }

        var next = _pendingEvents
            .OrderByDescending(item => item.Priority)
            .First();
        _pendingEvents.Remove(next);
        _activeEvent = next;
    }

    private void RemoveExpiredEvents()
    {
        RemoveDisallowedEvents();
        var now = GetUtcNow();
        if (_activeEvent?.ExpiresAtUtc <= now)
        {
            _activeEvent = null;
            _expiredEvents++;
        }

        _expiredEvents += _pendingEvents.RemoveAll(item => item.ExpiresAtUtc <= now);
        if (_activeEvent is null)
        {
            PromoteNextEvent();
        }

        var hasQueuedEvents = _activeEvent is not null || _pendingEvents.Count > 0;
        RefreshTemporarySelectionExpiration(
            reset: !hasQueuedEvents && _isTemporarySelectionExpirationSuspended);
    }

    private ModuleDisplayState? ResolveCurrentDisplay()
    {
        RemoveExpiredEvents();
        if (_activeEvent is { } activeEvent && FindModule(activeEvent.ModuleId) is { } eventModule)
        {
            return new ModuleDisplayState(eventModule.Descriptor, activeEvent.Presentation, activeEvent);
        }

        if (_isDefaultManuallySelected)
        {
            return null;
        }

        if (_manuallySelectedModuleId is { } selectedId &&
            GetAvailableModules().FirstOrDefault(item => item.Descriptor.Id == selectedId) is { } selected)
        {
            return selected;
        }

        return ResolvePersistentDisplay();
    }

    private ModuleDisplayState? GetManuallySelectedDisplay()
    {
        if (_manuallySelectedModuleId is not { } selectedId)
        {
            return null;
        }

        return GetAvailableModules().FirstOrDefault(item => item.Descriptor.Id == selectedId);
    }

    private void RefreshTemporarySelectionExpiration(bool reset)
    {
        if (GetManuallySelectedDisplay() is not { KeepsManualSelection: false })
        {
            CancelTemporarySelectionExpiration();
            return;
        }

        if (_activeEvent is not null || _pendingEvents.Count > 0)
        {
            SuspendTemporarySelectionExpiration();
            return;
        }

        if (reset || _temporarySelectionExpiresAtUtc is null)
        {
            ScheduleTemporarySelectionExpiration();
        }
    }

    private void ScheduleTemporarySelectionExpiration()
    {
        _isTemporarySelectionExpirationSuspended = false;
        _temporarySelectionExpiresAtUtc = GetUtcNow().Add(_temporarySelectionDuration);
    }

    private void CancelTemporarySelectionExpiration()
    {
        _isTemporarySelectionExpirationSuspended = false;
        _temporarySelectionExpiresAtUtc = null;
    }

    private void SuspendTemporarySelectionExpiration()
    {
        _isTemporarySelectionExpirationSuspended = true;
        _temporarySelectionExpiresAtUtc = null;
    }

    private bool ExpireTemporarySelectionIfDue()
    {
        if (_temporarySelectionExpiresAtUtc is not { } expiresAtUtc)
        {
            return false;
        }

        var remaining = expiresAtUtc - GetUtcNow();
        if (remaining > TimeSpan.Zero)
        {
            return false;
        }

        if (GetManuallySelectedDisplay() is { KeepsManualSelection: true })
        {
            CancelTemporarySelectionExpiration();
            return false;
        }

        _manuallySelectedModuleId = null;
        _isDefaultManuallySelected = true;
        CancelTemporarySelectionExpiration();
        return true;
    }

    private ModuleDisplayState? ResolvePersistentDisplay() => GetAvailableModules()
        .Where(item => item.Presentation.IsPersistentOverride ?? item.Descriptor.IsPersistent)
        .OrderByDescending(item => item.Presentation.PersistentPriorityOverride ?? item.Descriptor.PersistentPriority)
        .ThenBy(item => item.Descriptor.Id, StringComparer.Ordinal)
        .FirstOrDefault();

    private List<ModuleDisplayState> GetAvailableModules() => _registry.Modules
        .Where(module => module.IsEnabled &&
                         module.LifecycleState == ModuleLifecycleState.Active &&
                         module.CurrentPresentation is not null &&
                         AllowsModule(module.Descriptor.Id))
        .Select(module => new ModuleDisplayState(module.Descriptor, module.CurrentPresentation!))
        .OrderByDescending(item => item.Descriptor.PersistentPriority)
        .ThenBy(item => item.Descriptor.Id, StringComparer.Ordinal)
        .ToList();

    private IIslandModule? FindModule(string moduleId) => _registry.Modules.FirstOrDefault(
        module => module.Descriptor.Id.Equals(moduleId, StringComparison.Ordinal) &&
                  module.IsEnabled &&
                  module.LifecycleState == ModuleLifecycleState.Active &&
                  AllowsModule(module.Descriptor.Id));

    private void RemoveDisallowedEvents()
    {
        if (_activeEvent is not null && !AllowsEvent(_activeEvent))
        {
            _activeEvent = null;
        }

        _pendingEvents.RemoveAll(moduleEvent => !AllowsEvent(moduleEvent));
        if (_activeEvent is null)
        {
            PromoteNextEvent();
        }
    }

    private bool AllowsModule(string moduleId) =>
        _focusPolicy?.Current.AllowsModule(moduleId) ?? true;

    private bool AllowsEvent(ModuleEvent moduleEvent) =>
        _focusPolicy?.Current.AllowsEvent(moduleEvent) ?? true;

    private DateTimeOffset GetUtcNow() => _timeProvider.GetUtcNow();

    private static void ValidateTemporarySelectionDuration(TimeSpan duration)
    {
        if (duration < MinimumTemporarySelectionDuration ||
            duration > MaximumTemporarySelectionDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                $"Temporary selection duration must be between {MinimumTemporarySelectionDuration} and {MaximumTemporarySelectionDuration}.");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
