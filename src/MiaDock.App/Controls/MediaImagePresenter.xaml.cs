using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MiaDock.Modules.Media.Models;

namespace MiaDock.App.Controls;

public sealed partial class MediaImagePresenter : UserControl
{
    private long _loadVersion;
    private CancellationTokenSource? _loadCancellation;

    public static readonly DependencyProperty MediaProperty = DependencyProperty.Register(
        nameof(Media),
        typeof(MediaImage),
        typeof(MediaImagePresenter),
        new PropertyMetadata(null, OnMediaChanged));

    public MediaImagePresenter()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public MediaImage? Media
    {
        get => (MediaImage?)GetValue(MediaProperty);
        set => SetValue(MediaProperty, value);
    }

    private static void OnMediaChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var presenter = (MediaImagePresenter)dependencyObject;
        presenter.StartLoad((MediaImage?)args.NewValue);
    }

    private void StartLoad(MediaImage? media)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        _ = LoadAsync(media, _loadCancellation.Token);
    }

    private async Task LoadAsync(MediaImage? media, CancellationToken cancellationToken)
    {
        var version = Interlocked.Increment(ref _loadVersion);
        ImageElement.Source = null;
        if (media is null || !media.HasContent)
        {
            return;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            BitmapImage bitmap;
            if (media.Uri is not null)
            {
                bitmap = new BitmapImage(media.Uri);
            }
            else
            {
                using var memoryStream = new MemoryStream(media.Bytes!, writable: false);
                using var randomAccessStream = memoryStream.AsRandomAccessStream();
                bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(randomAccessStream).AsTask(cancellationToken);
            }

            if (!cancellationToken.IsCancellationRequested &&
                version == Interlocked.Read(ref _loadVersion))
            {
                ImageElement.Source = bitmap;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (version == Interlocked.Read(ref _loadVersion))
            {
                ImageElement.Source = null;
            }
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (ImageElement.Source is null && Media is { HasContent: true } media)
        {
            StartLoad(media);
        }
    }
}
