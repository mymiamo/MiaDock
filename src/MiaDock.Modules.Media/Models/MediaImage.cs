namespace MiaDock.Modules.Media.Models;

public sealed record MediaImage(
    string CacheKey,
    Uri? Uri,
    byte[]? Bytes,
    string? ContentType)
{
    public bool HasContent => Uri is not null || Bytes is { Length: > 0 };

    public static MediaImage FromUri(string cacheKey, Uri uri) => new(cacheKey, uri, null, null);

    public static MediaImage FromBytes(string cacheKey, byte[] bytes, string? contentType) =>
        new(cacheKey, null, bytes, contentType);
}
