using MiaDock.Modules.Media.Models;
using MiaDock.Platform.Windows.Media;

namespace MiaDock.Platform.Windows.Tests.Media;

[TestClass]
public sealed class MediaImageCacheTests
{
    [TestMethod]
    public void TryAdd_WhenCapacityIsExceeded_EvictsLeastRecentlyUsedImage()
    {
        var cache = new MediaImageCache(capacity: 2, maximumImageBytes: 16);
        cache.TryAdd(Create("first"));
        cache.TryAdd(Create("second"));
        cache.TryGet("first", out _);

        cache.TryAdd(Create("third"));

        Assert.IsTrue(cache.TryGet("first", out _));
        Assert.IsFalse(cache.TryGet("second", out _));
        Assert.IsTrue(cache.TryGet("third", out _));
    }

    [TestMethod]
    public void TryAdd_WhenImageExceedsLimit_RejectsImage()
    {
        var cache = new MediaImageCache(capacity: 2, maximumImageBytes: 2);

        var added = cache.TryAdd(MediaImage.FromBytes("large", new byte[3], "image/png"));

        Assert.IsFalse(added);
        Assert.AreEqual(0, cache.Count);
    }

    private static MediaImage Create(string key) =>
        MediaImage.FromBytes(key, new byte[] { 1 }, "image/png");
}
