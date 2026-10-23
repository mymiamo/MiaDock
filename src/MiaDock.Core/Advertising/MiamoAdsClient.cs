using System.Net;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MiaDock.Core.Advertising;

/// <summary>Public endpoints only. No credentials, cookies, disk cache, or unrestricted image redirects.</summary>
public sealed class MiamoAdsClient(HttpClient httpClient, bool ownsClient = false) : IMiamoAdsClient, IDisposable
{
    private static readonly Uri AdsEndpoint = new("https://mymiamo.net/api/v1/miadock-ads");
    private static readonly Uri ImpressionsEndpoint = new("https://mymiamo.net/api/v1/miadock-impressions");
    private const int MaximumImageBytes = 4 * 1024 * 1024;
    private readonly object _cacheGate = new();
    private readonly Dictionary<Uri, (byte[] Data, long Stored, long Access)> _cache = [];
    private long _access;

    public static MiamoAdsClient Create() => new(new HttpClient(new HttpClientHandler
    {
        AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    }) { Timeout = Timeout.InfiniteTimeSpan }, ownsClient: true);

    public async Task<IReadOnlyList<MiamoAd>> GetAdsAsync(CancellationToken cancellationToken)
    {
        using var timeout = RequestTimeout(cancellationToken);
        using var response = await GetTrustedAsync(AdsEndpoint, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await ReadBoundedAsync(response.Content, 512 * 1024, timeout.Token).ConfigureAwait(false);
        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("ads", out var ads) || ads.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Invalid public advertisement list.");
        var result = new List<MiamoAd>();
        foreach (var item in ads.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            string? Field(string key) => item.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
            var campaign = Field("campaign_id"); var token = Field("impression_token");
            if (!Uri.TryCreate(Field("creative_url"), UriKind.Absolute, out var creative) ||
                !Uri.TryCreate(Field("click_url"), UriKind.Absolute, out var click) || campaign is null || token is null) continue;
            var ad = new MiamoAd(campaign, creative, click, token);
            if (MiamoAdPolicy.IsValid(ad)) result.Add(ad);
        }
        return result.DistinctBy(ad => ad.Key).ToArray();
    }

    public async Task<byte[]> GetImageAsync(Uri uri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!MiamoAdPolicy.IsTrusted(uri)) throw new InvalidDataException("Untrusted creative origin.");
        lock (_cacheGate)
        {
            if (_cache.TryGetValue(uri, out var entry) && Stopwatch.GetElapsedTime(entry.Stored) < TimeSpan.FromMinutes(5))
            {
                _cache[uri] = (entry.Data, entry.Stored, ++_access);
                return entry.Data;
            }
        }
        using var timeout = RequestTimeout(cancellationToken);
        using var response = await GetTrustedAsync(uri, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var type = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
        if (type is not ("image/png" or "image/jpeg" or "image/webp" or "image/gif" or "image/bmp"))
            throw new InvalidDataException("Unsupported creative media type.");
        var bytes = await ReadBoundedAsync(response.Content, MaximumImageBytes, timeout.Token).ConfigureAwait(false);
        lock (_cacheGate)
        {
            _cache[uri] = (bytes, Stopwatch.GetTimestamp(), ++_access);
            while (_cache.Count > 8 || _cache.Values.Sum(entry => (long)entry.Data.Length) > 16 * 1024 * 1024)
                _cache.Remove(_cache.MinBy(entry => entry.Value.Access).Key);
        }
        return bytes;
    }

    public async Task<ImpressionResult> PostImpressionAsync(MiamoImpression impression, CancellationToken cancellationToken)
    {
        using var timeout = RequestTimeout(cancellationToken);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ImpressionsEndpoint)
            {
                Content = JsonContent.Create(new { campaign_id = impression.CampaignId, event_id = impression.EventId, impression_token = impression.ImpressionToken })
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            return new(response.IsSuccessStatusCode, status >= 500 || status is 408 or 429);
        }
        catch (HttpRequestException) { return new(false, true); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(false, true); }
    }

    private async Task<HttpResponseMessage> GetTrustedAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (var hop = 0; hop < 4; hop++)
        {
            if (!MiamoAdPolicy.IsTrusted(uri)) throw new InvalidDataException("Untrusted redirect origin.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var responseScope = new ResponseOwner(await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false));
            var response = responseScope.Response!;
            var status = (int)response.StatusCode;
            if (status is not (301 or 302 or 303 or 307 or 308)) return responseScope.Detach();
            var location = response.Headers.Location;
            if (location is null || !Uri.TryCreate(uri, location, out var next) || !MiamoAdPolicy.IsTrusted(next))
                throw new InvalidDataException("Untrusted advertisement redirect.");
            uri = next;
        }
        throw new InvalidDataException("Too many advertisement redirects.");
    }

    private static CancellationTokenSource RequestTimeout(CancellationToken token)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        return timeout;
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int limit, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > limit) throw new InvalidDataException("Advertisement response exceeds the size limit.");
        using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(block, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + count > limit) throw new InvalidDataException("Advertisement response exceeds the size limit.");
            buffer.Write(block, 0, count);
        }
        return buffer.ToArray();
    }

    private sealed class ResponseOwner(HttpResponseMessage response) : IDisposable
    {
        public HttpResponseMessage? Response { get; private set; } = response;
        public HttpResponseMessage Detach() { var response = Response!; Response = null; return response; }
        public void Dispose() => Response?.Dispose();
    }

    public void Dispose()
    {
        lock (_cacheGate) _cache.Clear();
        if (ownsClient) httpClient.Dispose();
    }
}
