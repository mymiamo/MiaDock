using System.Collections.Concurrent;
using Windows.ApplicationModel;
using Windows.Foundation;
using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Media;

internal sealed class WindowsAppIdentityResolver
{
    private readonly ConcurrentDictionary<string, MediaSourceInfo> _cache = new(StringComparer.Ordinal);
    private readonly MediaImageReader _imageReader;

    public WindowsAppIdentityResolver(MediaImageReader imageReader)
    {
        _imageReader = imageReader;
    }

    public async Task<MediaSourceInfo> ResolveAsync(
        string sourceAppUserModelId,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(sourceAppUserModelId, out var cached))
        {
            return cached;
        }

        var displayName = CreateFallbackDisplayName(sourceAppUserModelId);
        MediaImage? icon = null;

        try
        {
            var appInfo = AppInfo.GetFromAppUserModelId(sourceAppUserModelId);
            if (!string.IsNullOrWhiteSpace(appInfo.DisplayInfo.DisplayName))
            {
                displayName = appInfo.DisplayInfo.DisplayName;
            }

            icon = await _imageReader.ReadAsync(
                appInfo.DisplayInfo.GetLogo(new Size(32, 32)),
                $"source:{sourceAppUserModelId}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Desktop media providers do not always expose package identity.
        }

        var resolved = new MediaSourceInfo(sourceAppUserModelId, displayName, icon);
        _cache.TryAdd(sourceAppUserModelId, resolved);
        return resolved;
    }

    internal static string CreateFallbackDisplayName(string sourceAppUserModelId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppUserModelId))
        {
            return "Unknown media app";
        }

        var candidate = sourceAppUserModelId.Split('!').Last();
        candidate = Path.GetFileNameWithoutExtension(candidate);
        return string.IsNullOrWhiteSpace(candidate) ? sourceAppUserModelId : candidate;
    }
}
