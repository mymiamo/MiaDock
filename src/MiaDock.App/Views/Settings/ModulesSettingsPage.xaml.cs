using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Controls;
using MiaDock.App.Services;
using MiaDock.App.ViewModels;

namespace MiaDock.App.Views.Settings;

public sealed partial class ModulesSettingsPage : UserControl
{
    private readonly IAppLocalizationService? _localization;

    public ModulesSettingsPage(IAppLocalizationService? localization = null)
    {
        _localization = localization;
        InitializeComponent();
    }

    public event EventHandler<string>? DetailsRequested;

    private async void OnModuleToggleRequested(object sender, ModuleSettingsToggleEventArgs args)
    {
        if (DataContext is not SettingsViewModel viewModel) return;

        if (args.IsEnabled)
        {
            var localization = _localization ?? new AppLocalizationService();
            var disclosure = ModuleServiceDisclosureCatalog.Get(
                args.Item.ModuleId,
                localization);
            if (!disclosure.RequiresWindowsPermission)
            {
                var dialog = new Dialogs.ModuleServiceConsentDialog(
                    [disclosure],
                    localization,
                    isOnboarding: false)
                {
                    XamlRoot = XamlRoot
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    (sender as ModuleSettingsCard)?.SynchronizeToggle();
                    return;
                }
            }
        }

        await viewModel.SetModuleEnabledAsync(args.Item.ModuleId, args.IsEnabled);
        (sender as ModuleSettingsCard)?.SynchronizeToggle();
    }

    private void OnModuleDetailsRequested(object sender, ModuleSettingsDetailsEventArgs args) =>
        DetailsRequested?.Invoke(this, args.ModuleId);

    private async void OnShowServicesClick(
        object sender,
        RoutedEventArgs args)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var localization = _localization ?? new AppLocalizationService();
        var disclosures = viewModel.ModuleItems
            .Select(item => ModuleServiceDisclosureCatalog.Get(
                item.ModuleId,
                localization))
            .ToArray();
        var dialog = new Dialogs.ModuleServiceConsentDialog(
            disclosures,
            localization,
            isOnboarding: false,
            isReviewOnly: true)
        {
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }

}
