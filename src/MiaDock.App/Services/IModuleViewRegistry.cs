using Microsoft.UI.Xaml;

namespace MiaDock.App.Services;

public interface IModuleViewRegistry
{
    void Register(string viewKey, Func<FrameworkElement> factory);

    FrameworkElement? Create(string viewKey);
}
