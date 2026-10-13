using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Balikobot.Codes;

namespace Balikobot.Tests;

public class BatchLabelsTests
{
    private const string BaseUrl = "http://127.0.0.1:43123";

    [Fact]
    public async Task DownloadBatchLabelsReturnsTheSinglePdf()
    {
        string? body = null;
        var handler = new FakeHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/ppl/labels")
            {
                body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return Json($$"""{"status":200,"labels_url":"{{BaseUrl}}/labels.bin"}""");
            }

            return Bytes("%PDF-1.4 single label"u8.ToArray(), "application/pdf");
        });
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var label = await client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]);

        Assert.Equal("application/pdf", label.MediaType);
        Assert.Equal("%PDF-1.4 single label"u8.ToArray(), label.Bytes);
        Assert.Contains("\"package_ids\":[\"add-ppl-1\"]", body);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("/ppl/labels", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("/labels.bin", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.False(handler.Requests[1].Headers.ContainsKey("Authorization"));
    }

    [Fact]
    public async Task DownloadBatchLabelsReturnsTheZipArchiveForSeveralPackages()
    {
        var archive = "PK\x03\x04zip archive bytes"u8.ToArray();
        var handler = LabelHandler(archive, "application/zip");
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var label = await client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1", "add-ppl-2"]);

        Assert.Equal("application/zip", label.MediaType);
        Assert.Equal(archive, label.Bytes);
    }

    [Fact]
    public async Task DownloadBatchLabelsAcceptsZplForASinglePackage()
    {
        var zpl = "^XA^FO50,50^FDLABEL^FS^XZ"u8.ToArray();
        var handler = LabelHandler(zpl, "application/zpl");
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var label = await client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]);

        Assert.Equal("application/zpl", label.MediaType);
        Assert.Equal(zpl, label.Bytes);
    }

    [Fact]
    public async Task DownloadBatchLabelsValidatesBeforeNetwork()
    {
        var handler = new FakeHandler(_ => Json("{}"));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var tooMany = new string[1001];
        for (var index = 0; index < tooMany.Length; index++)
        {
            tooMany[index] = "add-ppl-" + index;
        }

        var cases = new (string Name, CarrierCode Carrier, IReadOnlyList<string> PackageIds)[]
        {
            ("missing ids", CarrierCode.PPL, []),
            ("empty id", CarrierCode.PPL, [""]),
            ("invalid id", CarrierCode.PPL, ["bad\nid"]),
            ("invalid carrier", CarrierCode.FromWire("PPL"), ["add-ppl-1"]),
            ("too many ids", CarrierCode.PPL, tooMany),
        };

        foreach (var (name, carrier, packageIds) in cases)
        {
            var exception = await Assert.ThrowsAsync<BalikobotException>(
                () => client.DownloadBatchLabelsAsync(carrier, packageIds));
            Assert.Equal(BalikobotError.InvalidRequest, exception.Error);
        }

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task DownloadBatchLabelsMapsRateLimitToUnavailableWithTheRetryHint()
    {
        var handler = new FakeHandler(_ =>
        {
            var response = Json("{}", HttpStatusCode.TooManyRequests);
            response.Headers.TryAddWithoutValidation("Retry-After", "30");
            return response;
        });
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.Unavailable, exception.Error);
        Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
    }

    [Fact]
    public async Task DownloadBatchLabelsMapsALookupServerErrorToUnavailable()
    {
        var handler = new FakeHandler(_ => Json("{}", HttpStatusCode.InternalServerError));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.Unavailable, exception.Error);
    }

    [Fact]
    public async Task DownloadBatchLabelsMapsALookupBadRequestToRejected()
    {
        var handler = new FakeHandler(_ => Json("{}", HttpStatusCode.BadRequest));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.Rejected, exception.Error);
    }

    [Fact]
    public async Task DownloadBatchLabelsMapsABodyTransientStatusToUnavailable()
    {
        var handler = new FakeHandler(_ => Json("""{"status":503}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.Unavailable, exception.Error);
    }

    [Fact]
    public async Task DownloadBatchLabelsMapsABodyRejectionToRejected()
    {
        var handler = new FakeHandler(_ => Json("""{"status":404}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.Rejected, exception.Error);
    }

    [Fact]
    public async Task DownloadBatchLabelsRejectsAMissingStatus()
    {
        var handler = new FakeHandler(_ => Json($$"""{"labels_url":"{{BaseUrl}}/labels.bin"}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    [Fact]
    public async Task DownloadBatchLabelsRejectsANonJsonLookupBody()
    {
        var handler = new FakeHandler(_ => Bytes("<html></html>"u8.ToArray(), "text/html"));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    [Fact]
    public async Task DownloadBatchLabelsRejectsAForeignLabelHost()
    {
        var handler = new FakeHandler(_ => Json(
            """{"status":200,"labels_url":"https://evil.example/labels.pdf"}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    [Fact]
    public async Task DownloadBatchLabelsRejectsZipForASinglePackage()
    {
        var handler = LabelHandler("PK\x03\x04archive"u8.ToArray(), "application/zip");
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    [Fact]
    public async Task DownloadBatchLabelsRejectsSingleDocumentMediaTypesForSeveralPackages()
    {
        foreach (var (body, mediaType) in new[]
                 {
                     ("%PDF-1.4 label"u8.ToArray(), "application/pdf"),
                     ("^XA^XZ"u8.ToArray(), "application/zpl"),
                 })
        {
            var handler = LabelHandler(body, mediaType);
            using var httpClient = new HttpClient(handler);
            using var client = CreateClient(httpClient);

            var exception = await Assert.ThrowsAsync<BalikobotException>(
                () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1", "add-ppl-2"]));

            Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
        }
    }

    [Fact]
    public async Task DownloadBatchLabelsRejectsABrokenMagicPrefix()
    {
        var cases = new (string Name, string[] Ids, byte[] Body, string MediaType)[]
        {
            ("broken pdf", ["add-ppl-1"], "not a pdf"u8.ToArray(), "application/pdf"),
            ("broken zpl", ["add-ppl-1"], "not a zpl"u8.ToArray(), "application/zpl"),
            ("broken zip", ["add-ppl-1", "add-ppl-2"], "%PDF-1.4 label"u8.ToArray(), "application/zip"),
        };

        foreach (var (name, ids, body, mediaType) in cases)
        {
            var handler = LabelHandler(body, mediaType);
            using var httpClient = new HttpClient(handler);
            using var client = CreateClient(httpClient);

            var exception = await Assert.ThrowsAsync<BalikobotException>(
                () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ids));

            Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
        }
    }

    [Fact]
    public async Task DownloadBatchLabelsRejectsAnEmptyBody()
    {
        var handler = LabelHandler([], "application/pdf");
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    [Fact]
    public async Task DownloadBatchLabelsRejectsAnUnexpectedContentType()
    {
        var handler = LabelHandler("<html></html>"u8.ToArray(), "text/html");
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    [Fact]
    public async Task DownloadBatchLabelsMapsADownloadNotFoundToRejected()
    {
        var handler = LabelHandler(Array.Empty<byte>(), "application/json", HttpStatusCode.NotFound);
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.Rejected, exception.Error);
    }

    [Fact]
    public async Task DownloadBatchLabelsMapsADownloadServerErrorToUnavailable()
    {
        var handler = LabelHandler(Array.Empty<byte>(), "application/json", HttpStatusCode.InternalServerError);
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.Unavailable, exception.Error);
    }

    [Fact]
    public async Task DownloadBatchLabelsRejectsABodyOverTheLabelLimit()
    {
        var oversized = new byte[(4 * 1024 * 1024) + 1];
        "%PDF-"u8.CopyTo(oversized);
        var handler = LabelHandler(oversized, "application/pdf");
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadBatchLabelsAsync(CarrierCode.PPL, ["add-ppl-1"]));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    private static FakeHandler LabelHandler(
        byte[] body,
        string mediaType,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        return new FakeHandler(request => request.RequestUri!.AbsolutePath == "/ppl/labels"
            ? Json($$"""{"status":200,"labels_url":"{{BaseUrl}}/labels.bin"}""")
            : Bytes(body, mediaType, status));
    }

    private static BalikobotClient CreateClient(HttpClient httpClient)
    {
        return new BalikobotClient(new BalikobotConfig
        {
            BaseUrl = BaseUrl,
            User = "api-user",
            ApiKey = "provider-secret",
            HttpClient = httpClient,
        });
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    private static HttpResponseMessage Bytes(
        byte[] body,
        string mediaType,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(body),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return response;
    }
}
