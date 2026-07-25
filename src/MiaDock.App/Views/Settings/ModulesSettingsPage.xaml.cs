using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Controls;
using MiaDock.App.ViewModels;

namespace MiaDock.App.Views.Settings;

public sealed partial class ModulesSettingsPage : UserControl
{
    public ModulesSettingsPage() => InitializeComponent();

    public event EventHandler<string>? DetailsRequested;

    private async void OnModuleToggleRequested(object sender, ModuleSettingsToggleEventArgs args)
    {
        if (DataContext is not SettingsViewModel viewModel) return;

        if (args.Item.ModuleId == "notifications" && args.IsEnabled)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Bildirim erişimine izin verilsin mi?",
                Content = "MiaDock kaynak uygulama ve bildirim başlığını okuyacak. Gövde metni ayrıca açılmadıkça gösterilmez; içerik teknik loglara yazılmaz.",
                PrimaryButtonText = "Windows iznini iste",
                CloseButtonText = "Vazgeç",
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
}
