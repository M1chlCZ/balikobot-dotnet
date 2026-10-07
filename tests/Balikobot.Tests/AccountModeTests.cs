using System.Net;
using System.Text;
using Balikobot.Codes;

namespace Balikobot.Tests;

public class AccountModeTests
{
    private const string BaseUrl = "http://127.0.0.1:43123";

    [Fact]
    public async Task MismatchedLiveAccountBlocksShipmentWritesBeforeTheyAreSent()
    {
        var handler = new FakeHandler(_ => Json(
            """{"status":200,"live_account":false,"carriers":[]}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient, liveAccount: true);

        var add = await Assert.ThrowsAsync<BalikobotException>(
            () => client.AddPackageAsync(CarrierCode.PPL, Request()));
        var order = await Assert.ThrowsAsync<BalikobotException>(
            () => client.OrderBatchAsync(CarrierCode.PPL, "12345"));

        Assert.Equal(BalikobotError.Unavailable, add.Error);
        Assert.Equal(BalikobotError.Unavailable, order.Error);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
            Assert.Equal("/info/whoami", request.RequestUri!.AbsolutePath));
    }

    [Fact]
    public async Task MatchingLiveAccountAllowsWritesAndCachesTheCheck()
    {
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/info/whoami" => Json("""{"status":200,"live_account":true,"carriers":[]}"""),
            "/ppl/add" => Json(
                """
                {"status":200,"packages":[{"eid":"ORDER-0001","status":200,"package_id":"P-1",
                "carrier_id":"TRACK-1","label_url":"http://127.0.0.1:43123/label.pdf"}]}
                """),
            _ => Json("""{"status":200,"order_id":"BB12345600152024001"}"""),
        });
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient, liveAccount: true);

        await client.AddPackageAsync(CarrierCode.PPL, Request());
        await client.OrderBatchAsync(CarrierCode.PPL, "12345");

        Assert.Equal(3, handler.Requests.Count);
        Assert.Single(handler.Requests, request =>
            request.RequestUri!.AbsolutePath == "/info/whoami");
    }

    [Fact]
    public async Task OrderPickupWithUnverifiedAccountIsRejected()
    {
        var handler = new FakeHandler(_ => Json(
            """{"status":200,"live_account":false,"carriers":[]}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient, liveAccount: true);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.OrderPickupAsync(CarrierCode.DPD, PickupRequest()));

        Assert.Equal(BalikobotError.Rejected, exception.Error);
        Assert.Single(handler.Requests);
        Assert.Equal("/info/whoami", handler.LastRequest.RequestUri!.AbsolutePath);
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

    private static PickupRequest PickupRequest()
    {
        return new PickupRequest
        {
            Date = "2026-09-14",
            WeightKg = 12.5,
            PackageCount = 3,
        };
    }

    private static BalikobotClient CreateClient(HttpClient httpClient, bool liveAccount)
    {
        return new BalikobotClient(new BalikobotConfig
        {
            BaseUrl = BaseUrl,
            User = "api-user",
            ApiKey = "provider-secret",
            HttpClient = httpClient,
            LiveAccount = liveAccount,
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
