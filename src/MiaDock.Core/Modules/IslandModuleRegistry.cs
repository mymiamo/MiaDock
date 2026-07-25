namespace MiaDock.Core.Modules;

public sealed class IslandModuleRegistry : IIslandModuleRegistry
{
    private readonly IReadOnlyList<IIslandModule> _modules;
    private bool _initialized;

    public IslandModuleRegistry(IEnumerable<IIslandModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _modules = modules.OrderByDescending(module => module.Descriptor.Priority).ToArray();
        if (_modules.Select(module => module.Descriptor.Id).Distinct(StringComparer.Ordinal).Count() != _modules.Count)
        {
            throw new InvalidOperationException("Island module identifiers must be unique.");
        }
    }

    public IReadOnlyList<IIslandModule> Modules => _modules;

    public IIslandModule? ActiveModule => _modules.FirstOrDefault(module =>
        module.IsEnabled &&
        module.LifecycleState == ModuleLifecycleState.Active &&
        module.CurrentPresentation is not null);

    public ModulePresentation? ActivePresentation => ActiveModule?.CurrentPresentation;

    public event EventHandler<ModulePresentation?>? ActivePresentationChanged;

    public event EventHandler<ModuleEvent>? ModuleEventOccurred;

    public bool CanExecuteCommand(string moduleId, string commandId) =>
        Modules.FirstOrDefault(module => module.Descriptor.Id == moduleId) is { } module &&
        module.IsEnabled &&
        module.LifecycleState == ModuleLifecycleState.Active &&
        module.CanExecuteCommand(commandId);

    public ValueTask<bool> ExecuteCommandAsync(
        string moduleId,
        string commandId,
        CancellationToken cancellationToken = default)
    {
        var module = Modules.FirstOrDefault(candidate => candidate.Descriptor.Id == moduleId);
        return module is null || !module.IsEnabled || module.LifecycleState != ModuleLifecycleState.Active
            ? ValueTask.FromResult(false)
            : module.ExecuteCommandAsync(commandId, cancellationToken);
    }

    public async ValueTask<bool> SetEnabledAsync(
        string moduleId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var module = Modules.FirstOrDefault(candidate => candidate.Descriptor.Id == moduleId);
        if (module is null) return false;
        if (module.IsEnabled == enabled &&
            (!_initialized || module.LifecycleState == (enabled ? ModuleLifecycleState.Active : ModuleLifecycleState.Inactive)))
        {
            return true;
        }

        if (!enabled && _initialized && module.LifecycleState == ModuleLifecycleState.Active)
        {
            await module.DeactivateAsync(cancellationToken);
        }

        module.IsEnabled = enabled;
        if (enabled && _initialized && module.LifecycleState != ModuleLifecycleState.Active)
        {
            await module.ActivateAsync(cancellationToken);
        }

        ActivePresentationChanged?.Invoke(this, ActivePresentation);
        return true;
    }

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        foreach (var module in _modules)
        {
            module.PresentationChanged += OnPresentationChanged;
            module.EventOccurred += OnEventOccurred;
            if (module.IsEnabled)
            {
                await module.ActivateAsync(cancellationToken);
            }
        }

        _initialized = true;
        ActivePresentationChanged?.Invoke(this, ActivePresentation);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var module in _modules)
        {
            module.PresentationChanged -= OnPresentationChanged;
            module.EventOccurred -= OnEventOccurred;
            if (module.LifecycleState == ModuleLifecycleState.Active)
            {
                await module.DeactivateAsync();
            }
        }

        _initialized = false;
    }

    private void OnPresentationChanged(object? sender, ModulePresentation? presentation) =>
        ActivePresentationChanged?.Invoke(this, ActivePresentation);

    private void OnEventOccurred(object? sender, ModuleEvent moduleEvent)
    {
        ActivePresentationChanged?.Invoke(this, ActivePresentation);
        ModuleEventOccurred?.Invoke(this, moduleEvent);
    }
}
