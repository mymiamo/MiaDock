using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.App.ViewModels;
using MiaDock.Core.Applications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Windows.Storage.Pickers;
using System.Text.Json;

namespace MiaDock.App.Views.Settings;

public sealed partial class DiagnosticsSettingsPage : UserControl
{
    private static readonly Uri BugReportUri = new("https://mymiamo.net/bug");

    private readonly DiagnosticsViewModel _viewModel;
    private readonly IDiagnosticsFileService _fileService;
    private readonly Window _owner;
    private readonly IAppLocalizationService? _localization;
    private readonly IExternalUriLauncher? _uriLauncher;
    private DispatcherQueueTimer? _performanceTimer;

    public DiagnosticsSettingsPage(
        DiagnosticsViewModel viewModel,
        IDiagnosticsFileService fileService,
        Window owner,
        IAppLocalizationService? localization = null,
        IExternalUriLauncher? uriLauncher = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _fileService = fileService;
        _owner = owner;
        _localization = localization;
        _uriLauncher = uriLauncher;
        DataContext = viewModel;
        MeasureAnimationButton.Content = localization?.Text("60 saniye akıcılık ölç", "Measure smoothness for 60 seconds") ?? "60 saniye akıcılık ölç";
        StopAnimationButton.Content = localization?.Text("Ölçümü durdur", "Stop measurement") ?? "Ölçümü durdur";
        Loaded += OnLoaded;
        Unloaded += (_, _) => { _performanceTimer?.Stop(); _performanceTimer = null; };
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        _performanceTimer?.Stop();
        _performanceTimer = DispatcherQueue.CreateTimer();
        _performanceTimer.Interval = TimeSpan.FromSeconds(1);
        _performanceTimer.Tick += (_, _) => _viewModel.RefreshPerformance();
        _performanceTimer.Start();
        await _viewModel.RefreshAsync();
    }

    private async void OnPerformanceExportClick(object sender, RoutedEventArgs args)
    {
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = $"MiaDock-performance-{DateTime.Now:yyyyMMdd-HHmmss}" };
            picker.FileTypeChoices.Add("JSON", [".json"]);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(_owner));
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            var snapshot = App.Services?.GetService<ApplicationPerformanceMonitor>()?.Snapshot();
            if (snapshot is not null) await File.WriteAllTextAsync(file.Path,
                JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        { _viewModel.ReportFileOperationFailure(); }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs args) => await _viewModel.RefreshAsync();
    private void OnMeasureAnimationClick(object sender, RoutedEventArgs args) => _viewModel.StartAnimationMeasurement();
    private void OnStopAnimationClick(object sender, RoutedEventArgs args) => _viewModel.StopAnimationMeasurement();

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
            Title = Text("Dialog.Logs.Clear.Title", "Yerel loglar temizlensin mi?"),
            Content = Text("Dialog.Logs.Clear.Description", "Bu işlem mevcut teknik log dosyalarını kalıcı olarak siler."),
            PrimaryButtonText = Text("Dialog.Logs.Clear.Action", "Logları temizle"),
            CloseButtonText = Text("Common.Cancel", "İptal"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await _viewModel.ClearAsync();
        }
    }

    private async void OnReportBugClick(object sender, RoutedEventArgs args)
    {
        var opened = false;
        try
        {
            if (_uriLauncher is not null)
            {
                opened = await _uriLauncher.LaunchAsync(BugReportUri);
            }
            else
            {
                opened = await Windows.System.Launcher.LaunchUriAsync(BugReportUri);
            }
        }
        catch (Exception)
        {
            // Launcher failures are reported in the UI and must never escape an event handler.
        }

        if (!opened)
        {
            await ShowBugLinkFailureAsync();
        }
    }

    private async Task ShowBugLinkFailureAsync()
    {
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = Text("Dialog.Link.OpenFailed.Title", "Bağlantı açılamadı"),
                Content = Text(
                    "Dialog.BugReport.OpenFailed.Description",
                    "Hata bildirim sayfası açılamadı. mymiamo.net/bug adresini tarayıcınızda açabilirsiniz."),
                CloseButtonText = Text("Common.Close", "Kapat"),
                DefaultButton = ContentDialogButton.Close
            };
            await dialog.ShowAsync();
        }
        catch (Exception)
        {
            // A dialog can be unavailable while the page is unloading; keep the action crash-safe.
        }
    }

    private string Text(string key, string fallback) =>
        _localization?.Get(key) is { } value && value != key ? value : fallback;
}
