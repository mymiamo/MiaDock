using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.Core.Settings;

namespace MiaDock.App.Dialogs;

public sealed partial class CloseBehaviorDialog : ContentDialog
{
    public CloseBehaviorDialog(IAppLocalizationService? localization = null)
    {
        InitializeComponent();
        if (localization is null)
        {
            return;
        }

        Title = localization.Get("Dialog.Close.Title");
        PrimaryButtonText = localization.Get("Dialog.Apply");
        CloseButtonText = localization.Get("Common.Cancel");
        DescriptionText.Text = localization.Get("Dialog.Close.Description");
        MinimizeOption.Content = localization.Get("Dialog.Close.Minimize");
        ExitOption.Content = localization.Get("Dialog.Close.Exit");
        RememberOption.Content = localization.Get("Dialog.Close.Remember");
    }

    public CloseBehaviorSetting SelectedBehavior => ExitOption.IsChecked == true
        ? CloseBehaviorSetting.Exit
        : CloseBehaviorSetting.MinimizeToTray;

    public bool RememberChoice => RememberOption.IsChecked == true;
}
