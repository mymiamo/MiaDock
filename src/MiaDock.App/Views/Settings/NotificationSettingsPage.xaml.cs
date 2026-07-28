using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.ViewModels;
using MiaDock.App.Services;

namespace MiaDock.App.Views.Settings;

public sealed partial class NotificationSettingsPage : UserControl
{
    private SettingsViewModel? _viewModel;
    private readonly IAppLocalizationService? _localization;
    private bool _synchronizing;

    public NotificationSettingsPage(IAppLocalizationService? localization = null)
    {
        _localization = localization;
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_viewModel is not null) return;
        _viewModel = DataContext as SettingsViewModel;
        if (_viewModel is null) return;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SynchronizeToggle();
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;
        Unloaded -= OnUnloaded;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SettingsViewModel.NotificationsEnabled)) SynchronizeToggle();
    }

    private void SynchronizeToggle()
    {
        if (_viewModel is null) return;
        _synchronizing = true;
        EnableToggle.IsOn = _viewModel.NotificationsEnabled;
        _synchronizing = false;
    }

    private async void OnEnableToggled(object sender, RoutedEventArgs args)
    {
        if (_synchronizing || _viewModel is null) return;
        if (!EnableToggle.IsOn)
        {
            await _viewModel.SetNotificationsEnabledAsync(false);
            return;
        }

        _synchronizing = true;
        EnableToggle.IsOn = false;
        _synchronizing = false;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Text("Dialog.Permission.Notification.Title", "Bildirim erişimine izin verilsin mi?"),
            Content = Text("Dialog.Permission.Notification.Detail", "MiaDock, Windows Bildirim Merkezi'ndeki uygulama adını ve başlığı okuyacak. Gövde metni yalnız uygulama bazında açılır; içerik loglanmaz ve bildirimler silinmez ya da okundu işaretlenmez."),
            PrimaryButtonText = Text("Dialog.Permission.Request", "Windows iznini iste"),
            CloseButtonText = Text("Dialog.Permission.Cancel", "Vazgeç"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await _viewModel.SetNotificationsEnabledAsync(true);
        }
        SynchronizeToggle();
    }

    private string Text(string key, string fallback) =>
        _localization?.Get(key) is { } value && value != key ? value : fallback;
}
