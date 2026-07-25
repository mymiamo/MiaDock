using Microsoft.UI.Xaml.Controls;
using MiaDock.Core.Settings;

namespace MiaDock.App.Dialogs;

public sealed partial class CloseBehaviorDialog : ContentDialog
{
    public CloseBehaviorDialog()
    {
        InitializeComponent();
    }

    public CloseBehaviorSetting SelectedBehavior => ExitOption.IsChecked == true
        ? CloseBehaviorSetting.Exit
        : CloseBehaviorSetting.MinimizeToTray;

    public bool RememberChoice => RememberOption.IsChecked == true;
}
