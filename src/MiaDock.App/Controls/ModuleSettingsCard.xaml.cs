using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class ModuleSettingsCard : UserControl
{
    public ModuleSettingsCard() => InitializeComponent();

    public event EventHandler<ModuleSettingsToggleEventArgs>? ToggleRequested;
    public event EventHandler<ModuleSettingsDetailsEventArgs>? DetailsRequested;

    public void SynchronizeToggle()
    {
        if (DataContext is ModuleSettingsItemViewModel item)
        {
            EnabledToggle.IsOn = item.IsEnabled;
        }
    }

    private void OnEnabledToggled(object sender, RoutedEventArgs args)
    {
        if (DataContext is ModuleSettingsItemViewModel item && EnabledToggle.IsOn != item.IsEnabled)
        {
            ToggleRequested?.Invoke(this, new(item, EnabledToggle.IsOn));
        }
    }

    private void OnDetailsClick(object sender, RoutedEventArgs args)
    {
        if (DataContext is ModuleSettingsItemViewModel item)
        {
            DetailsRequested?.Invoke(this, new(item.ModuleId));
        }
    }
}

public sealed class ModuleSettingsToggleEventArgs(
    ModuleSettingsItemViewModel item,
    bool isEnabled) : EventArgs
{
    public ModuleSettingsItemViewModel Item { get; } = item;
    public bool IsEnabled { get; } = isEnabled;
}

public sealed class ModuleSettingsDetailsEventArgs(string moduleId) : EventArgs
{
    public string ModuleId { get; } = moduleId;
}
