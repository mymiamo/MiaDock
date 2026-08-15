using MiaDock.Core.Clipboard;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace MiaDock.Platform.Windows.Clipboard;

internal enum ClipboardPlatformContentKind
{
    Text,
    StorageItems,
    Image
}

internal sealed record ClipboardPlatformStorageItem(string Name, string? Path, bool IsFolder);

internal sealed record ClipboardPlatformImage(
    int Width,
    int Height,
    byte[]? ThumbnailPng,
    byte[]? FullPng);

internal sealed record ClipboardPlatformSnapshot(
    ClipboardPlatformContentKind Kind,
    string? Text = null,
    IReadOnlyList<ClipboardPlatformStorageItem>? StorageItems = null,
    ClipboardPlatformImage? Image = null);

internal interface IClipboardPlatformAdapter : IAsyncDisposable
{
    event EventHandler? ContentChanged;

    void Start();

    void Stop();

    Task<ClipboardPlatformSnapshot?> ReadAsync(CancellationToken cancellationToken);

    Task WriteTextAsync(string text, CancellationToken cancellationToken);

    Task<ClipboardPeekActionResult> SavePngAsync(
        byte[] png,
        nint ownerWindow,
        CancellationToken cancellationToken);
}

internal sealed class WindowsClipboardPlatformAdapter : IClipboardPlatformAdapter
{
    private const int MaximumThumbnailPixels = 320;
    private const int MaximumThumbnailBytes = 512 * 1024;
    private const long MaximumImagePixels = 64L * 1024 * 1024;
    private const int MaximumImagePayloadBytes = 20 * 1024 * 1024;
    private bool _started;

    public event EventHandler? ContentChanged;

    public void Start()
    {
        if (_started) return;
        global::Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged += OnContentChanged;
        _started = true;
    }

    public void Stop()
    {
        if (!_started) return;
        global::Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged -= OnContentChanged;
        _started = false;
    }

    public async Task<ClipboardPlatformSnapshot?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (content.Contains(StandardDataFormats.StorageItems))
        {
            var storageItems = await content.GetStorageItemsAsync().AsTask(cancellationToken).ConfigureAwait(false);
            var items = storageItems.Select(item => new ClipboardPlatformStorageItem(
                item.Name,
                string.IsNullOrWhiteSpace(item.Path) ? null : item.Path,
                item.IsOfType(StorageItemTypes.Folder))).ToArray();
            return items.Length == 0 ? null : new(ClipboardPlatformContentKind.StorageItems, StorageItems: items);
        }

        if (content.Contains(StandardDataFormats.Bitmap))
        {
            var image = await ReadImageAsync(content, cancellationToken).ConfigureAwait(false);
            return image is null ? null : new(ClipboardPlatformContentKind.Image, Image: image);
        }

        if (content.Contains(StandardDataFormats.Text))
        {
            var text = await content.GetTextAsync().AsTask(cancellationToken).ConfigureAwait(false);
            return new(ClipboardPlatformContentKind.Text, Text: text);
        }

        return null;
    }

    public Task WriteTextAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var package = new DataPackage();
        package.SetText(text);
        var options = new ClipboardContentOptions
        {
            IsAllowedInHistory = false,
            IsRoamable = false
        };
        global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContentWithOptions(package, options);
        return Task.CompletedTask;
    }

    public async Task<ClipboardPeekActionResult> SavePngAsync(
        byte[] png,
        nint ownerWindow,
        CancellationToken cancellationToken)
    {
        if (png.Length == 0 || ownerWindow == 0) return ClipboardPeekActionResult.Unavailable;
        cancellationToken.ThrowIfCancellationRequested();
        var picker = new FileSavePicker { SuggestedFileName = "Clipboard-image" };
        picker.FileTypeChoices.Add("PNG", [".png"]);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, ownerWindow);
        var target = await picker.PickSaveFileAsync().AsTask(cancellationToken).ConfigureAwait(false);
        if (target is null) return ClipboardPeekActionResult.Cancelled;
        await FileIO.WriteBytesAsync(target, png).AsTask(cancellationToken).ConfigureAwait(false);
        return ClipboardPeekActionResult.Succeeded;
    }

    private void OnContentChanged(object? sender, object args) =>
        ContentChanged?.Invoke(this, EventArgs.Empty);

    private static async Task<ClipboardPlatformImage?> ReadImageAsync(
        DataPackageView content,
        CancellationToken cancellationToken)
    {
        var reference = await content.GetBitmapAsync().AsTask(cancellationToken).ConfigureAwait(false);
        using var stream = await reference.OpenReadAsync().AsTask(cancellationToken).ConfigureAwait(false);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
        var width = checked((int)decoder.PixelWidth);
        var height = checked((int)decoder.PixelHeight);
        if (width <= 0 || height <= 0 || (long)width * height > MaximumImagePixels) return null;

        var thumbnail = await EncodePngAsync(decoder, width, height, thumbnail: true, cancellationToken)
            .ConfigureAwait(false);
        stream.Seek(0);
        decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
        var full = await EncodePngAsync(decoder, width, height, thumbnail: false, cancellationToken)
            .ConfigureAwait(false);
        if (full is { Length: > MaximumImagePayloadBytes }) full = null;
        return new ClipboardPlatformImage(width, height, thumbnail, full);
    }

    private static async Task<byte[]?> EncodePngAsync(
        BitmapDecoder decoder,
        int width,
        int height,
        bool thumbnail,
        CancellationToken cancellationToken)
    {
        var scale = thumbnail
            ? Math.Min(1d, MaximumThumbnailPixels / (double)Math.Max(width, height))
            : 1d;
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Max(1, Math.Round(width * scale)),
            ScaledHeight = (uint)Math.Max(1, Math.Round(height * scale))
        };
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb)
            .AsTask(cancellationToken).ConfigureAwait(false);
        using var output = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output)
            .AsTask(cancellationToken).ConfigureAwait(false);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
        if (output.Size > int.MaxValue || (thumbnail && output.Size > MaximumThumbnailBytes)) return null;
        output.Seek(0);
        using var reader = new DataReader(output.GetInputStreamAt(0));
        await reader.LoadAsync((uint)output.Size).AsTask(cancellationToken).ConfigureAwait(false);
        var bytes = new byte[(int)output.Size];
        reader.ReadBytes(bytes);
        return bytes;
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }
}
