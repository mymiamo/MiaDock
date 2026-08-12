using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MiaDock.Modules.SystemStatus.ViewModels;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace MiaDock.App.Controls;

public sealed partial class PrivacyAppIconView : UserControl
{
    private CancellationTokenSource? _loadCancellation;

    public PrivacyAppIconView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        _ = LoadIconAsync(_loadCancellation.Token);
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
    }

    private async Task LoadIconAsync(CancellationToken cancellationToken)
    {
        if (DataContext is not PrivacyApplicationItemViewModel viewModel ||
            string.IsNullOrWhiteSpace(viewModel.ExecutablePath))
        {
            return;
        }

        try
        {
            var path = viewModel.ExecutablePath;
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
            {
                return;
            }

            var file = await StorageFile.GetFileFromPathAsync(path);
            cancellationToken.ThrowIfCancellationRequested();
            using var thumbnail = await file.GetThumbnailAsync(
                ThumbnailMode.SingleItem,
                40,
                ThumbnailOptions.UseCurrentScale);
            cancellationToken.ThrowIfCancellationRequested();
            if (thumbnail.Size == 0)
            {
                return;
            }

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
        }
    }
}
