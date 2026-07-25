using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Media;

public sealed class MediaImageCache
{
    private readonly object _sync = new();
    private readonly int _capacity;
    private readonly int _maximumImageBytes;
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<CacheEntry> _leastRecentlyUsed = new();

    public MediaImageCache(int capacity = 64, int maximumImageBytes = 5 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumImageBytes, 1);
        _capacity = capacity;
        _maximumImageBytes = maximumImageBytes;
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _entries.Count;
            }
        }
    }

    public bool TryGet(string cacheKey, out MediaImage? image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        lock (_sync)
        {
            if (!_entries.TryGetValue(cacheKey, out var node))
            {
                image = null;
                return false;
            }

            _leastRecentlyUsed.Remove(node);
            _leastRecentlyUsed.AddFirst(node);
            image = node.Value.Image;
            return true;
        }
    }

    public bool TryAdd(MediaImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Bytes is not { Length: > 0 } bytes || bytes.Length > _maximumImageBytes)
        {
            return false;
        }

        lock (_sync)
        {
            if (_entries.TryGetValue(image.CacheKey, out var existing))
            {
                _leastRecentlyUsed.Remove(existing);
                existing.Value = new CacheEntry(image.CacheKey, image);
                _leastRecentlyUsed.AddFirst(existing);
                return true;
            }

            var node = _leastRecentlyUsed.AddFirst(new CacheEntry(image.CacheKey, image));
            _entries.Add(image.CacheKey, node);

            while (_entries.Count > _capacity)
            {
                var last = _leastRecentlyUsed.Last!;
                _leastRecentlyUsed.RemoveLast();
                _entries.Remove(last.Value.CacheKey);
            }

            return true;
        }
    }

    private sealed record CacheEntry(string CacheKey, MediaImage Image);
}
