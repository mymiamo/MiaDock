using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Dialogs;
using MiaDock.App.Services;
using MiaDock.App.ViewModels;

namespace MiaDock.App.Views.Settings;

public sealed partial class FocusSettingsPage : UserControl
{
    private readonly FocusSettingsViewModel _viewModel;
    private readonly IAppLocalizationService _localization;

    public FocusSettingsPage(
        FocusSettingsViewModel viewModel,
        IAppLocalizationService localization)
    {
        _viewModel = viewModel;
        _localization = localization;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnAddProfileClick(object sender, RoutedEventArgs args)
    {
        using var editor = _viewModel.CreateNewEditor();
        if (editor is null)
        {
            await ShowMessageAsync(
                Text("Focus.Settings.Error.Limit"));
            return;
        }

        await ShowEditorAsync(editor);
    }

    private async void OnEditProfileClick(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement { Tag: string profileId })
        {
            return;
        }

        using var editor = _viewModel.CreateEditor(profileId);
        if (editor is not null)
        {
            await ShowEditorAsync(editor);
        }
    }

    private async void OnDeleteProfileClick(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement { Tag: string profileId })
        {
            return;
        }

        var item = _viewModel.Profiles.FirstOrDefault(profile =>
            profile.Id.Equals(profileId, StringComparison.Ordinal));
        if (item is null)
        {
            return;
        }

        var message = _viewModel.IsActive(profileId)
            ? Text("Focus.Settings.Delete.ActiveMessage")
            : Text("Focus.Settings.Delete.Message");
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Text("Focus.Settings.Delete.Title"),
            Content = message,
            PrimaryButtonText = Text("Focus.Settings.Delete.Action"),
            CloseButtonText = Text("Common.Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _viewModel.Delete(profileId);
        }
    }

    private async void OnResetProfileClick(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement { Tag: string profileId })
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Text("Focus.Settings.Reset.Title"),
            Content = Text("Focus.Settings.Reset.Message"),
            PrimaryButtonText = Text("Common.Reset"),
            CloseButtonText = Text("Common.Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _viewModel.ResetBuiltIn(profileId);
        }
    }

    private async Task ShowEditorAsync(FocusProfileEditorViewModel editor)
    {
        var dialog = new FocusProfileEditorDialog(editor, _localization)
        {
            XamlRoot = XamlRoot
        };
        dialog.PrimaryButtonClick += OnPrimaryButtonClick;
        try
        {
            await dialog.ShowAsync();
        }
        finally
        {
            dialog.PrimaryButtonClick -= OnPrimaryButtonClick;
        }

        void OnPrimaryButtonClick(
            ContentDialog sender,
            ContentDialogButtonClickEventArgs args)
        {
            var result = _viewModel.Save(editor);
            if (result != FocusProfileSaveResult.Success)
            {
                args.Cancel = true;
                if (!editor.HasError)
                {
                    editor.SetError(result switch
                    {
                        FocusProfileSaveResult.DuplicateName =>
                            "Focus.Settings.Error.DuplicateName",
                        FocusProfileSaveResult.LimitReached =>
                            "Focus.Settings.Error.Limit",
                        _ => "Focus.Settings.Error.Invalid"
                    });
                }
            }
        }
    }

    private async Task ShowMessageAsync(string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Text("Focus.Title"),
            Content = message,
            CloseButtonText = Text("Common.Cancel")
        };
        await dialog.ShowAsync();
    }

    private string Text(string key) => _localization.Get(key);
}
