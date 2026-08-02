using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MiaDock.Modules.SystemStatus.ViewModels;
using System.Runtime.InteropServices;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace MiaDock.App.Controls;

public sealed partial class AudioSessionIconView : UserControl
{
    private CancellationTokenSource? _loadCancellation;

    public AudioSessionIconView() => InitializeComponent();

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
        if (DataContext is not AudioMixerSessionViewModel viewModel ||
            string.IsNullOrWhiteSpace(viewModel.Snapshot.IconPath))
        {
            return;
        }

        try
        {
            var path = viewModel.Snapshot.IconPath;
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
            {
                return;
            }

            var file = await StorageFile.GetFileFromPathAsync(path);
            cancellationToken.ThrowIfCancellationRequested();
            using var thumbnail = await file.GetThumbnailAsync(
                ThumbnailMode.SingleItem,
                48,
                ThumbnailOptions.UseCurrentScale);
            cancellationToken.ThrowIfCancellationRequested();
            if (thumbnail.Size == 0)
            {
                return;
            }

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(thumbnail);
            cancellationToken.ThrowIfCancellationRequested();
            SessionIcon.Source = bitmap;
            SessionIcon.Visibility = Visibility.Visible;
            FallbackIcon.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) when (!IsFatal(exception))
        {
            // Protected processes and resource icon strings use the safe glyph fallback.
        }
    }

    private static bool IsFatal(Exception exception) =>
        exception is OutOfMemoryException or StackOverflowException or AccessViolationException;
}
