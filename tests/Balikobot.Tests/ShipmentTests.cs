using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Balikobot.Codes;

namespace Balikobot.Tests;

public class ShipmentTests
{
    private const string BaseUrl = "http://127.0.0.1:43123";
    private const string LabelUrl = BaseUrl + "/label.pdf";

    [Fact]
    public async Task AddPackageReturnsTheAcceptedRecord()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {"status":200,"packages":[{"eid":"ORDER-0001","status":200,"package_id":"P-1",
            "carrier_id":"TRACK-1","label_url":"http://127.0.0.1:43123/label.pdf"}]}
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var result = await client.AddPackageAsync(CarrierCode.PPL, Request());

        Assert.Equal("P-1", result.PackageId);
        Assert.Equal("TRACK-1", result.CarrierId);
        Assert.Equal(LabelUrl, result.LabelUrl);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("/ppl/add", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task AddPackageTreatsAnAlreadyStoredEidAsSuccess()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {"status":208,"packages":[{"eid":"ORDER-0001","status":208,"package_id":"P-1",
            "carrier_id":"TRACK-1","label_url":"http://127.0.0.1:43123/label.pdf"}]}
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var result = await client.AddPackageAsync(CarrierCode.PPL, Request());

        Assert.Equal("P-1", result.PackageId);
    }

    [Fact]
    public async Task AddPackageRejectsAnInvalidEidLocally()
    {
        var handler = new FakeHandler(_ => Json("{}"));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.AddPackageAsync(CarrierCode.PPL, Request() with { Eid = "short" }));

        Assert.Equal(BalikobotError.InvalidRequest, exception.Error);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AddPackageMapsAClientErrorToRejected()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.AddPackageAsync(CarrierCode.PPL, Request()));

        Assert.Equal(BalikobotError.Rejected, exception.Error);
    }

    [Fact]
    public async Task AddPackageWithNaNWeightIsAmbiguousAndSendsNoRequest()
    {
        var handler = new FakeHandler(_ => Json("{}"));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.AddPackageAsync(CarrierCode.PPL, Request() with { WeightKg = double.NaN }));

        Assert.Equal(BalikobotError.Ambiguous, exception.Error);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AddPackageTreatsAnOversizedServerErrorBodyAsUnavailable()
    {
        var handler = new FakeHandler(_ => Bytes(new byte[64], "application/json", HttpStatusCode.InternalServerError));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient, maxResponseBytes: 10);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.AddPackageAsync(CarrierCode.PPL, Request()));

        Assert.Equal(BalikobotError.Unavailable, exception.Error);
    }

    [Fact]
    public async Task AddPackageRejectsNullRequiredAddressFields()
    {
        var handler = new FakeHandler(_ => Json("{}"));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var requests = new[]
        {
            Request() with { RecStreet = null! },
            Request() with { RecCity = null! },
            Request() with { RecZip = null! },
        };

        foreach (var request in requests)
        {
            var exception = await Assert.ThrowsAsync<BalikobotException>(
                () => client.AddPackageAsync(CarrierCode.PPL, request));
            Assert.Equal(BalikobotError.InvalidRequest, exception.Error);
        }

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void ValidFieldTreatsNullAsInvalid()
    {
        Assert.False(ShipmentWire.ValidField(null!, 10));
        Assert.False(ShipmentWire.ValidPackageId(null));
    }

    [Fact]
    public async Task AddPackageOmitsEmptyOptionalStrings()
    {
        string? body = null;
        var handler = new FakeHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json(
                """
                {"status":200,"packages":[{"eid":"ORDER-0001","status":200,"package_id":"P-1",
                "carrier_id":"TRACK-1","label_url":"http://127.0.0.1:43123/label.pdf"}]}
                """);
        });
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        await client.AddPackageAsync(CarrierCode.PPL, Request() with
        {
            RecName = "",
            RecFirm = "F",
            RecPhone = "",
            RecEmail = "a@example.test",
            BranchId = "",
        });

        Assert.DoesNotContain("\"rec_name\"", body);
        Assert.DoesNotContain("\"rec_phone\"", body);
        Assert.DoesNotContain("\"branch_id\"", body);
        Assert.Contains("\"rec_firm\":\"F\"", body);
        Assert.Contains("\"rec_email\":\"a@example.test\"", body);
    }

    [Fact]
    public async Task AddPackageSerializesTheCodesAsPlainStrings()
    {
        string? body = null;
        var handler = new FakeHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json(
                """
                {"status":200,"packages":[{"eid":"ORDER-0001","status":200,"package_id":"P-1",
                "carrier_id":"TRACK-1","label_url":"http://127.0.0.1:43123/label.pdf"}]}
                """);
        });
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        await client.AddPackageAsync(CarrierCode.PPL, Request());

        Assert.Contains("\"rec_country\":\"CZ\"", body);
        Assert.Contains("\"cod_currency\":\"CZK\"", body);
    }

    [Fact]
    public void CodeStructsSerializeAsPlainStrings()
    {
        Assert.Equal("\"ppl\"", JsonSerializer.Serialize(CarrierCode.PPL));
        Assert.Equal("\"CZK\"", JsonSerializer.Serialize(CurrencyCode.CZK));
        Assert.Equal("\"CZ\"", JsonSerializer.Serialize(CountryCode.CZ));
        Assert.Equal(CountryCode.CZ, JsonSerializer.Deserialize<CountryCode>("\"CZ\""));
    }

    [Fact]
    public async Task OverviewReturnsTheOpenPackages()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {"status":200,"packages":[{"eid":"ORDER-0001","package_id":"P-1",
            "carrier_id":"TRACK-1","label_url":"http://127.0.0.1:43123/label.pdf"}]}
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var packages = await client.OverviewAsync(CarrierCode.PPL, "ORDER-0001");

        var package = Assert.Single(packages);
        Assert.Equal("ORDER-0001", package.Eid);
        Assert.Equal("P-1", package.PackageId);
        Assert.Equal("TRACK-1", package.CarrierId);
        Assert.Equal(LabelUrl, package.LabelUrl);
        Assert.Equal("/ppl/overview", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task OverviewSkipsForeignInvalidEntriesAndFailsTheMatchingOne()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {"status":200,"packages":[
              {"eid":"FOREIGN","package_id":"P-9","carrier_id":"","label_url":"http://127.0.0.1:43123/x.pdf"},
              {"eid":"ORDER-0001","package_id":"P-1","carrier_id":"TRACK-1","label_url":"http://127.0.0.1:43123/label.pdf"}
            ]}
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var packages = await client.OverviewAsync(CarrierCode.PPL, "ORDER-0001");

        var package = Assert.Single(packages);
        Assert.Equal("ORDER-0001", package.Eid);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.OverviewAsync(CarrierCode.PPL, "FOREIGN"));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    [Fact]
    public async Task LabelsReturnsTheAggregateLabelUrl()
    {
        var handler = new FakeHandler(_ => Json(
            """{"status":200,"labels_url":"http://127.0.0.1:43123/labels.pdf"}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var labelUrl = await client.LabelsAsync(CarrierCode.PPL, "P-1");

        Assert.Equal(BaseUrl + "/labels.pdf", labelUrl);
        Assert.Equal("/ppl/labels", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task LabelsRejectsAForeignLabelUrl()
    {
        var handler = new FakeHandler(_ => Json(
            """{"status":200,"labels_url":"http://example.test/labels.pdf"}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.LabelsAsync(CarrierCode.PPL, "P-1"));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    [Fact]
    public async Task OrderViewLabelsReturnsTheClosedOrderLabelUrl()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {"status":200,"order_id":"ORDER-1","package_ids":["P-1"],
            "labels_url":"http://127.0.0.1:43123/order.pdf"}
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var labelUrl = await client.OrderViewLabelsAsync(CarrierCode.PPL, "ORDER-1", "P-1");

        Assert.Equal(BaseUrl + "/order.pdf", labelUrl);
        Assert.Equal("/ppl/orderview/ORDER-1", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task OrderViewLabelsRejectsAPackageThatIsNotAMember()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {"status":200,"order_id":"ORDER-1","package_ids":["P-2"],
            "labels_url":"http://127.0.0.1:43123/order.pdf"}
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.OrderViewLabelsAsync(CarrierCode.PPL, "ORDER-1", "P-1"));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    [Fact]
    public async Task DownloadLabelReturnsThePdfBody()
    {
        var handler = new FakeHandler(_ => Bytes("%PDF-1.4 test"u8.ToArray(), "application/pdf"));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var label = await client.DownloadLabelAsync(LabelUrl);

        Assert.Equal("application/pdf", label.MediaType);
        Assert.Equal("%PDF-1.4 test"u8.ToArray(), label.Bytes);
        Assert.False(handler.LastRequest.Headers.ContainsKey("Authorization"));
        Assert.False(handler.LastRequest.Headers.ContainsKey("Accept"));
    }

    [Fact]
    public async Task DownloadLabelRejectsAWrongMediaType()
    {
        var handler = new FakeHandler(_ => Bytes("not a label"u8.ToArray(), "text/plain"));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DownloadLabelAsync(LabelUrl));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    [Fact]
    public void ResolveBranchIdFollowsTheCarrierRules()
    {
        Assert.Equal("11000", BalikobotClient.ResolveBranchId(CarrierCode.CP, "NP", "BR-1", "110 00"));
        Assert.Equal("11000", BalikobotClient.ResolveBranchId(CarrierCode.ULOZENKA, "CP_NP", "BR-1", "110 00"));
        Assert.Equal("BR-1", BalikobotClient.ResolveBranchId(CarrierCode.ULOZENKA, "OTHER", "BR-1", "110 00"));
        Assert.Equal("123", BalikobotClient.ResolveBranchId(CarrierCode.PPL, "1", "KM123", ""));
    }

    private static AddPackageRequest Request()
    {
        return new AddPackageRequest
        {
            Eid = "ORDER-0001",
            ServiceType = "1",
            RecName = "Jan Novák",
            RecStreet = "Psí 1",
            RecCity = "Praha",
            RecZip = "11000",
            RecCountry = CountryCode.CZ,
            RecPhone = "+420123456789",
            WeightKg = 1.5,
            LengthCm = 20,
            WidthCm = 10,
            HeightCm = 5,
            Price = 100,
            CodCurrency = CurrencyCode.CZK,
        };
    }

    private static BalikobotClient CreateClient(HttpClient httpClient, int maxResponseBytes = 0)
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
