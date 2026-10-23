namespace MiaDock.Core.Advertising;

public sealed record MiamoAd(string CampaignId, Uri CreativeUrl, Uri ClickUrl, string ImpressionToken)
{
    public string Key => $"{CampaignId}\n{CreativeUrl.AbsoluteUri}\n{ClickUrl.AbsoluteUri}";
}

public sealed record MiamoImpression(string CampaignId, Guid EventId, string ImpressionToken);
public sealed record ImpressionResult(bool Accepted, bool Retryable);

public interface IMiamoAdsClient
{
    Task<IReadOnlyList<MiamoAd>> GetAdsAsync(CancellationToken cancellationToken);
    Task<byte[]> GetImageAsync(Uri uri, CancellationToken cancellationToken);
    Task<ImpressionResult> PostImpressionAsync(MiamoImpression impression, CancellationToken cancellationToken);
}

public static class MiamoAdPolicy
{
    public static bool IsTrusted(Uri? uri) => uri is { IsAbsoluteUri: true } &&
        uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) &&
        (uri.IdnHost.Equals("mymiamo.net", StringComparison.OrdinalIgnoreCase) ||
         uri.IdnHost.Equals("www.mymiamo.net", StringComparison.OrdinalIgnoreCase));

    public static bool IsValid(MiamoAd ad) => !string.IsNullOrWhiteSpace(ad.CampaignId) && ad.CampaignId.Length <= 128 &&
        !string.IsNullOrWhiteSpace(ad.ImpressionToken) && ad.ImpressionToken.Length <= 4096 &&
        IsTrusted(ad.CreativeUrl) && IsTrusted(ad.ClickUrl);
}

public static class AdPanelLayout
{
    public static double ImageSide(double availableWidth, double availableHeight, double chromeHeight)
    {
        if (!double.IsFinite(availableWidth) || !double.IsFinite(availableHeight) || !double.IsFinite(chromeHeight)) return 0;
        var side = Math.Floor(Math.Min(240, Math.Min(availableWidth - 24, availableHeight - Math.Max(0, chromeHeight))));
        return side >= 96 ? side : 0;
    }
}
