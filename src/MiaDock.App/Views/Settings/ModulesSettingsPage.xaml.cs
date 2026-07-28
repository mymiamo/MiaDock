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

        if (args.Item.ModuleId == "notifications" && args.IsEnabled)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = Text("Dialog.Permission.Notification.Title", "Bildirim erişimine izin verilsin mi?"),
                Content = Text("Dialog.Permission.Notification.Summary", "MiaDock kaynak uygulama ve bildirim başlığını okuyacak. Gövde metni ayrıca açılmadıkça gösterilmez; içerik teknik loglara yazılmaz."),
                PrimaryButtonText = Text("Dialog.Permission.Request", "Windows iznini iste"),
                CloseButtonText = Text("Dialog.Permission.Cancel", "Vazgeç"),
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                (sender as ModuleSettingsCard)?.SynchronizeToggle();
                return;
            }
        }

        await viewModel.SetModuleEnabledAsync(args.Item.ModuleId, args.IsEnabled);
        (sender as ModuleSettingsCard)?.SynchronizeToggle();
    }

    private void OnModuleDetailsRequested(object sender, ModuleSettingsDetailsEventArgs args) =>
        DetailsRequested?.Invoke(this, args.ModuleId);

    private string Text(string key, string fallback) =>
        _localization?.Get(key) is { } value && value != key ? value : fallback;
}
