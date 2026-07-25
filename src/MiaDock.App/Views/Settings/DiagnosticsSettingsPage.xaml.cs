using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.App.ViewModels;

namespace MiaDock.App.Views.Settings;

public sealed partial class DiagnosticsSettingsPage : UserControl
{
    private readonly DiagnosticsViewModel _viewModel;
    private readonly IDiagnosticsFileService _fileService;
    private readonly Window _owner;

    public DiagnosticsSettingsPage(
        DiagnosticsViewModel viewModel,
        IDiagnosticsFileService fileService,
        Window owner)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _fileService = fileService;
        _owner = owner;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args) => await _viewModel.RefreshAsync();

    private async void OnRefreshClick(object sender, RoutedEventArgs args) => await _viewModel.RefreshAsync();

    private async void OnOpenFolderClick(object sender, RoutedEventArgs args)
    {
        try
        {
            _ = await _fileService.OpenLogFolderAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _viewModel.ReportFileOperationFailure();
        }
    }

    private async void OnExportClick(object sender, RoutedEventArgs args)
    {
        try
        {
            _viewModel.ReportExportResult(await _fileService.PickAndExportAsync(_owner));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _viewModel.ReportFileOperationFailure();
        }
    }

    private async void OnClearClick(object sender, RoutedEventArgs args)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Yerel loglar temizlensin mi?",
            Content = "Bu işlem mevcut teknik log dosyalarını kalıcı olarak siler.",
            PrimaryButtonText = "Logları temizle",
            CloseButtonText = "İptal",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await _viewModel.ClearAsync();
        }
    }
}
