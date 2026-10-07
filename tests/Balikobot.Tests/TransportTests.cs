using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Balikobot.Tests;

public class TransportTests
{
    private const string BaseUrl = "http://127.0.0.1:43123";

    [Fact]
    public void LoopbackHttpBaseUrlIsAccepted()
    {
        using var client = new BalikobotClient(new BalikobotConfig
        {
            BaseUrl = BaseUrl,
            User = "api-user",
            ApiKey = "key",
        });
    }

    [Fact]
    public void NonLoopbackHttpBaseUrlThrowsConfig()
    {
        var exception = Assert.Throws<BalikobotException>(() => new BalikobotClient(new BalikobotConfig
        {
            BaseUrl = "http://192.0.2.1:9000",
            User = "api-user",
            ApiKey = "key",
        }));

        Assert.Equal(BalikobotError.Config, exception.Error);
    }

    [Fact]
    public async Task RedirectResponseIsNotFollowed()
    {
        var handler = new FakeHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri("https://example.test/next");
            return response;
        });
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var raw = await Wire.RequestAsync(client, HttpMethod.Get, "/next", null, CancellationToken.None);

        Assert.Equal(302, raw.Status);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task BasicAuthAndAcceptHeadersReachTheHandler()
    {
        var handler = new FakeHandler(_ => Json("{}"));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        await Wire.RequestAsync(client, HttpMethod.Post, "/test", new { value = 1 }, CancellationToken.None);

        var request = handler.LastRequest;
        Assert.Equal(
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("api-user:provider-secret")),
            request.Headers["Authorization"].Single());
        Assert.Equal("application/json", request.Headers["Accept"].Single());
    }

    [Fact]
    public async Task IsJsonAcceptsAQuotedMediaTypeParameter()
    {
        var handler = new FakeHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("{}"u8.ToArray()),
            };
            response.Content.Headers.ContentType =
                MediaTypeHeaderValue.Parse("application/json; charset=\"utf-8\"");
            return response;
        });
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var raw = await Wire.RequestAsync(client, HttpMethod.Get, "/test", null, CancellationToken.None);

        Assert.True(Wire.IsJson(raw));
    }

    [Fact]
    public async Task BodyLargerThanTheLimitThrowsBodyLimit()
    {
        var handler = new FakeHandler(_ => Json(new string('a', 11)));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient, maxResponseBytes: 10);

        var exception = await Assert.ThrowsAsync<RequestFailureException>(
            () => Wire.RequestAsync(client, HttpMethod.Get, "/test", null, CancellationToken.None));

        Assert.Equal(RequestFailureKind.BodyLimit, exception.Kind);
    }

    [Fact]
    public async Task BodyExactlyAtTheLimitIsAccepted()
    {
        var handler = new FakeHandler(_ => Json(new string('a', 10)));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient, maxResponseBytes: 10);

        var raw = await Wire.RequestAsync(client, HttpMethod.Get, "/test", null, CancellationToken.None);

        Assert.Equal(10, raw.Body.Length);
    }

    private static BalikobotClient CreateClient(HttpClient httpClient, int maxResponseBytes = 1024)
    {
        return new BalikobotClient(new BalikobotConfig
        {
            BaseUrl = BaseUrl,
            User = "api-user",
            ApiKey = "provider-secret",
            MaxResponseBytes = maxResponseBytes,
            HttpClient = httpClient,
        });
    }

    private static HttpResponseMessage Json(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }
}

/// <summary>Test handler with canned responses that captures requests. It bypasses the real
/// decompression pipeline, so in production the response limit applies to the decompressed stream.</summary>
internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    internal FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    internal List<CapturedRequest> Requests { get; } = [];

    internal CapturedRequest LastRequest => Requests[^1];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(new CapturedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase)));
        return Task.FromResult(_responder(request));
    }
}

internal sealed record CapturedRequest(
    HttpMethod Method,
    Uri? RequestUri,
    IReadOnlyDictionary<string, string[]> Headers);
