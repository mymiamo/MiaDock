using Microsoft.UI.Xaml;

namespace MiaDock.App.Services;

public sealed class ModuleViewRegistry : IModuleViewRegistry
{
    private readonly Dictionary<string, Func<FrameworkElement>> _factories =
        new(StringComparer.Ordinal);

    public void Register(string viewKey, Func<FrameworkElement> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewKey);
        ArgumentNullException.ThrowIfNull(factory);
        if (!_factories.TryAdd(viewKey, factory))
        {
            throw new InvalidOperationException($"A module view is already registered for '{viewKey}'.");
        }
    }

    public FrameworkElement? Create(string viewKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewKey);
        return _factories.TryGetValue(viewKey, out var factory) ? factory() : null;
    }
}
