using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MiaDock.Modules.SystemStatus.ViewModels;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace MiaDock.App.Controls;

public sealed partial class CallAppIconView : UserControl
{
    private CancellationTokenSource? _loadCancellation;
    private SystemActivityViewModel? _viewModel;

    public CallAppIconView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (DataContext is SystemActivityViewModel viewModel && !ReferenceEquals(_viewModel, viewModel))
        {
            if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        ReloadIcon();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        CancelLoad();
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SystemActivityViewModel.Snapshot)) ReloadIcon();
    }

    private void ReloadIcon()
    {
        CancelLoad();
        AppIcon.Source = null;
        AppIcon.Visibility = Visibility.Collapsed;
        FallbackIcon.Visibility = Visibility.Visible;
        var path = _viewModel?.Snapshot.CallActivity.ExecutablePath;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !File.Exists(path)) return;
        _loadCancellation = new CancellationTokenSource();
        _ = LoadIconAsync(path, _loadCancellation.Token);
    }

    private async Task LoadIconAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            cancellationToken.ThrowIfCancellationRequested();
            using var thumbnail = await file.GetThumbnailAsync(
                ThumbnailMode.SingleItem,
                64,
                ThumbnailOptions.UseCurrentScale);
            cancellationToken.ThrowIfCancellationRequested();
            if (thumbnail.Size == 0) return;
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(thumbnail);
            cancellationToken.ThrowIfCancellationRequested();
            AppIcon.Source = bitmap;
            AppIcon.Visibility = Visibility.Visible;
            FallbackIcon.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        {
            // Protected/package-backed executables keep the semantic Ionicons fallback.
        }
    }

    private void CancelLoad()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
    }
}
