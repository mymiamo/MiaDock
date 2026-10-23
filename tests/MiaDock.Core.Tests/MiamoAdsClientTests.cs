using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MiaDock.Core.Advertising;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class MiamoAdsClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Requests++; return send(request, token); }
    }
    private static HttpResponseMessage Response(HttpStatusCode status, string json = "") =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [TestMethod]
    public async Task ListFiltersInvalidAdsPreservesOrderAndUsesNoCredentials()
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.AreEqual("https://mymiamo.net/api/v1/miadock-ads", request.RequestUri!.AbsoluteUri);
            Assert.IsNull(request.Headers.Authorization); Assert.IsFalse(request.Headers.Contains("X-API-Key"));
            return Task.FromResult(Response(HttpStatusCode.OK, """
                {"ads":[
                  {"campaign_id":"a","creative_url":"https://mymiamo.net/a.png","click_url":"https://mymiamo.net/a","impression_token":"public-a"},
                  {"campaign_id":"bad","creative_url":"https://evil.example/a.png","click_url":"https://mymiamo.net/a","impression_token":"public-bad"},
                  {"campaign_id":"b","creative_url":"https://mymiamo.net/b.png","click_url":"https://www.mymiamo.net/b","impression_token":"public-b"},
                  {"campaign_id":"missing"},42]}
                """));
        });
        using var http = new HttpClient(handler); using var client = new MiamoAdsClient(http);
        var ads = await client.GetAdsAsync(CancellationToken.None);
        CollectionAssert.AreEqual(new[] { "a", "b" }, ads.Select(ad => ad.CampaignId).ToArray());
    }

    [TestMethod]
    public async Task PublicEndpoint404AndServerOutageAreHandledWithoutAnAuthenticationFallback()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Response(HttpStatusCode.NotFound)));
        using var http = new HttpClient(handler); using var client = new MiamoAdsClient(http);
        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => client.GetAdsAsync(CancellationToken.None));
        Assert.AreEqual(1, handler.Requests);
    }

    [TestMethod]
    public async Task EmptyListIsValidButMalformedEnvelopeIsNotAnEmptyCampaignList()
    {
        var malformed = false;
        using var handler = new Handler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, malformed ? "[]" : "{\"ads\":[]}")));
        using var http = new HttpClient(handler); using var client = new MiamoAdsClient(http);
        Assert.IsEmpty(await client.GetAdsAsync(CancellationToken.None));
        malformed = true;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => client.GetAdsAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task CreativeRedirectCannotLeaveTheAllowlistAndNoRequestIsSentToTheOtherHost()
    {
        using var handler = new Handler((_, _) =>
        {
            var response = Response(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("https://evil.example/tracker.png"); return Task.FromResult(response);
        });
        using var http = new HttpClient(handler); using var client = new MiamoAdsClient(http);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => client.GetImageAsync(new("https://mymiamo.net/a.png"), CancellationToken.None));
        Assert.AreEqual(1, handler.Requests);
    }

    [TestMethod]
    public async Task SameOriginRelativeRedirectIsAllowedAndCachedImagesAreBounded()
    {
        using var handler = new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/redirect")
            {
                var redirect = Response(HttpStatusCode.Redirect); redirect.Headers.Location = new Uri("/image.png", UriKind.Relative);
                return Task.FromResult(redirect);
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png"); return Task.FromResult(response);
        });
        using var http = new HttpClient(handler); using var client = new MiamoAdsClient(http);
        var uri = new Uri("https://mymiamo.net/redirect");
        await client.GetImageAsync(uri, CancellationToken.None); await client.GetImageAsync(uri, CancellationToken.None);
        Assert.AreEqual(2, handler.Requests);
        for (var i = 0; i < 9; i++) await client.GetImageAsync(new($"https://mymiamo.net/{i}.png"), CancellationToken.None);
        var before = handler.Requests; await client.GetImageAsync(uri, CancellationToken.None);
        Assert.AreEqual(before + 2, handler.Requests, "The oldest creative must be evicted from the eight-entry cache.");
    }

    [TestMethod]
    public async Task SvgAndOversizedImagesAreRejected()
    {
        var oversized = false;
        using var handler = new Handler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(oversized ? "image/png" : "image/svg+xml");
            if (oversized) response.Content.Headers.ContentLength = 4 * 1024 * 1024 + 1;
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler); using var client = new MiamoAdsClient(http);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => client.GetImageAsync(new("https://mymiamo.net/a"), CancellationToken.None));
        oversized = true;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => client.GetImageAsync(new("https://mymiamo.net/b"), CancellationToken.None));
    }

    [TestMethod]
    public async Task ImpressionPostsExactPublicContractRetainsEventIdAndDoesNotFollowRedirects()
    {
        var id = Guid.NewGuid(); var attempt = 0;
        using var handler = new Handler(async (request, token) =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual("https://mymiamo.net/api/v1/miadock-impressions", request.RequestUri!.AbsoluteUri);
            Assert.IsNull(request.Headers.Authorization);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.AreEqual(3, body.RootElement.EnumerateObject().Count());
            Assert.AreEqual("a", body.RootElement.GetProperty("campaign_id").GetString());
            Assert.AreEqual(id, body.RootElement.GetProperty("event_id").GetGuid());
            Assert.AreEqual("public-token", body.RootElement.GetProperty("impression_token").GetString());
            return Response(++attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NoContent);
        });
        using var http = new HttpClient(handler); using var client = new MiamoAdsClient(http);
        var impression = new MiamoImpression("a", id, "public-token");
        Assert.AreEqual(new ImpressionResult(false, true), await client.PostImpressionAsync(impression, CancellationToken.None));
        Assert.AreEqual(new ImpressionResult(true, false), await client.PostImpressionAsync(impression, CancellationToken.None));
    }

    [TestMethod]
    public async Task CancellationAbortsOutstandingRequests()
    {
        using var handler = new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Response(HttpStatusCode.OK); });
        using var http = new HttpClient(handler); using var client = new MiamoAdsClient(http);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetAdsAsync(cancellation.Token));
    }
}
