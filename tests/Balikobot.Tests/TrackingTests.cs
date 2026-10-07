using System.Net;
using System.Text;
using Balikobot.Codes;

namespace Balikobot.Tests;

public class TrackingTests
{
    private const string BaseUrl = "http://127.0.0.1:43123";

    [Fact]
    public async Task TrackStatusReturnsTheLatestProviderStatus()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {"status":200,"packages":[{"carrier_id":"TRACK-1","status_id":1,
            "status_id_v2":1.2,"name":"Delivered"}]}
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var result = await client.TrackStatusAsync(CarrierCode.PPL, "TRACK-1");

        Assert.Equal("1.2", result.StatusId);
        Assert.Equal("Delivered", result.StatusText);
        Assert.Equal("/ppl/trackstatus", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task TrackStatusMaps404ToNotFound()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.TrackStatusAsync(CarrierCode.PPL, "TRACK-1"));

        Assert.Equal(BalikobotError.NotFound, exception.Error);
    }

    [Fact]
    public async Task TrackStatusRejectsAMismatchedCarrierId()
    {
        var handler = new FakeHandler(_ => Json(
            """{"status":200,"packages":[{"carrier_id":"OTHER","status_id":1,"name":"Delivered"}]}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.TrackStatusAsync(CarrierCode.PPL, "TRACK-1"));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    [Fact]
    public async Task OrderBatchReturnsTheProviderOrderId()
    {
        var handler = new FakeHandler(_ => Json("""{"status":200,"order_id":"order-ppl-1"}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var result = await client.OrderBatchAsync(CarrierCode.PPL, "P-1");

        Assert.Equal("order-ppl-1", result.OrderId);
        Assert.Equal("/ppl/order", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task OrderBatchMapsABodyRejectionToRejected()
    {
        var handler = new FakeHandler(_ => Json("""{"status":400}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.OrderBatchAsync(CarrierCode.PPL, "P-1"));

        Assert.Equal(BalikobotError.Rejected, exception.Error);
    }

    [Fact]
    public async Task DropPackageTreatsABody404AsSuccess()
    {
        var handler = new FakeHandler(_ => Json("""{"status":404}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        await client.DropPackageAsync(CarrierCode.PPL, "P-1");

        Assert.Equal("/ppl/drop", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task DropPackageMapsABody405ToRejected()
    {
        var handler = new FakeHandler(_ => Json("""{"status":405}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.DropPackageAsync(CarrierCode.PPL, "P-1"));

        Assert.Equal(BalikobotError.Rejected, exception.Error);
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

    private static HttpResponseMessage Json(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }
}
