using System.Net;
using System.Text;
using Balikobot.Codes;

namespace Balikobot.Tests;

public class PickupTests
{
    private const string BaseUrl = "http://127.0.0.1:43123";

    [Fact]
    public async Task OrderPickupDpdConfirmsTheBooking()
    {
        string? body = null;
        var handler = new FakeHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""{"status":"200"}""");
        });
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var result = await client.OrderPickupAsync(CarrierCode.DPD, Request());

        Assert.True(result.Confirmed);
        Assert.Equal(string.Empty, result.ProviderId);
        Assert.Equal("/dpd/orderpickup", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"date\":\"2026-09-14\"", body);
        Assert.Contains("\"weight\":12.5", body);
        Assert.Contains("\"package_count\":3", body);
        Assert.Contains("\"message\":\"Leave at the warehouse.\"", body);
    }

    [Fact]
    public async Task OrderPickupPplReturnsTheProviderConfirmation()
    {
        var handler = new FakeHandler(_ => Json(
            """{"status":200,"pickup_order_id":"BB12345600152024001","confirmed":true}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var result = await client.OrderPickupAsync(CarrierCode.PPL, Request());

        Assert.True(result.Confirmed);
        Assert.Equal("BB12345600152024001", result.ProviderId);
        Assert.Equal("/ppl/orderpickup", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task OrderPickupRejectsAnUnsupportedCarrierWithoutARequest()
    {
        var handler = new FakeHandler(_ => Json("{}"));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.OrderPickupAsync(CarrierCode.CP, Request()));

        Assert.Equal(BalikobotError.Rejected, exception.Error);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task OrderPickupRejectsANullNoteWithoutARequest()
    {
        var handler = new FakeHandler(_ => Json("{}"));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.OrderPickupAsync(CarrierCode.DPD, Request() with { Note = null! }));

        Assert.Equal(BalikobotError.Rejected, exception.Error);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task OrderPickupRequiresThePplConfirmationFlag()
    {
        var handler = new FakeHandler(_ => Json(
            """{"status":200,"pickup_order_id":"BB12345600152024001"}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.OrderPickupAsync(CarrierCode.PPL, Request()));

        Assert.Equal(BalikobotError.Ambiguous, exception.Error);
    }

    private static PickupRequest Request()
    {
        return new PickupRequest
        {
            Date = "2026-09-14",
            WeightKg = 12.5,
            PackageCount = 3,
            Note = "Leave at the warehouse.",
        };
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
