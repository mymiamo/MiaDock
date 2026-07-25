using Windows.Storage.Streams;
using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Media;

internal sealed class MediaImageReader
{
    private const int MaximumImageBytes = 5 * 1024 * 1024;
    private readonly MediaImageCache _cache;

    public MediaImageReader(MediaImageCache cache)
    {
        _cache = cache;
    }

    public async Task<MediaImage?> ReadAsync(
        IRandomAccessStreamReference? reference,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        if (reference is null)
        {
            return null;
        }

        if (_cache.TryGet(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = await reference.OpenReadAsync().AsTask(cancellationToken);
            if (stream.Size is 0 or > MaximumImageBytes)
            {
                return null;
            }

            var requestedLength = checked((uint)stream.Size);
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            var loadedLength = await reader.LoadAsync(requestedLength).AsTask(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (loadedLength == 0)
            {
                return null;
            }

            var bytes = new byte[loadedLength];
            reader.ReadBytes(bytes);
            var image = MediaImage.FromBytes(cacheKey, bytes, stream.ContentType);
            _cache.TryAdd(image);
            return image;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
