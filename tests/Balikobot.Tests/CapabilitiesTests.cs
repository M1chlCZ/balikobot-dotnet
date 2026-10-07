using System.Net;
using System.Text;
using Balikobot.Codes;

namespace Balikobot.Tests;

public class CapabilitiesTests
{
    private const string BaseUrl = "http://127.0.0.1:43123";

    [Fact]
    public async Task WhoAmIAsyncReturnsTheAccountInformation()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {
              "status": 200,
              "live_account": true,
              "account": {"email": "private@example.test"},
              "carriers": [{"slug": "ppl", "name": "PPL"}, {"slug": "gls"}]
            }
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var whoami = await client.WhoAmIAsync();

        Assert.Equal(200, whoami.Status);
        Assert.True(whoami.LiveAccount);
        Assert.Equal(2, whoami.Carriers.Count);
        Assert.Equal(CarrierCode.PPL, whoami.Carriers[0].Slug);
        Assert.Equal("PPL", whoami.Carriers[0].Name);
        Assert.Equal(CarrierCode.GLS, whoami.Carriers[1].Slug);
        Assert.Equal(string.Empty, whoami.Carriers[1].Name);
        Assert.Equal("/info/whoami", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ActivatedServicesAsyncYieldsNoServicesWhenParcelShippingIsInactive()
    {
        var handler = new FakeHandler(_ => Json(
            """{"status":200,"active_parcel":false,"service_types":[{"service_type":"80","name":"Balíkovna"}]}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var activated = await client.ActivatedServicesAsync(CarrierCode.PPL);

        Assert.False(activated.ActiveParcel);
        Assert.Empty(activated.Services);
        Assert.Equal("/ppl/activatedservices", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CountriesAsyncAcceptsTheArrayShape()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {
              "status": 200,
              "service_types": [
                {"service_type": "VMCZ", "countries": [" cz ", "SK"]},
                {"service_type": 6830, "countries": ["AT"]}
              ]
            }
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var services = await client.CountriesAsync(CarrierCode.ZASILKOVNA);

        Assert.Equal(2, services.Count);
        Assert.Equal("VMCZ", services[0].ServiceType);
        Assert.Equal(new[] { CountryCode.CZ, CountryCode.SK }, services[0].Countries);
        Assert.Equal("6830", services[1].ServiceType);
        Assert.Equal(new[] { CountryCode.AT }, services[1].Countries);
    }

    [Fact]
    public async Task CountriesAsyncAcceptsTheSparseObjectShape()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {
              "status": 200,
              "service_types": {
                "7": {"service_type": 4, "countries": ["CZ"]},
                "2": {"service_type": 3, "countries": ["SK"]}
              }
            }
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var services = await client.CountriesAsync(CarrierCode.ZASILKOVNA);

        Assert.Equal(new[] { "3", "4" }, services.Select(service => service.ServiceType));
    }

    [Fact]
    public async Task CodAsyncTreatsHttp501AsAnEmptyDictionary()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotImplemented));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var services = await client.CodAsync(CarrierCode.ZASILKOVNA);

        Assert.Empty(services);
        Assert.Equal("/zasilkovna/cod4services", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CodAsyncTreatsAnOversized501BodyAsAnEmptyDictionary()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotImplemented)
        {
            Content = new StringContent(new string('a', 64), Encoding.UTF8, "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient, maxResponseBytes: 10);

        var services = await client.CodAsync(CarrierCode.ZASILKOVNA);

        Assert.Empty(services);
    }

    [Fact]
    public async Task CarrierCapabilitiesAsyncAggregatesServicesAndKeepsEuCountries()
    {
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/info/whoami" => Json(
                """{"status":200,"live_account":false,"carriers":[{"slug":"ppl","name":"PPL"}]}"""),
            "/ppl/activatedservices" => Json(
                """{"status":200,"service_types":[{"service_type":"HOME","name":" Home ","home_delivery":true}]}"""),
            "/ppl/countries4service" => Json(
                """{"status":200,"service_types":[{"service_type":"HOME","countries":["cz","DE","US"]}]}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var carriers = await client.CarrierCapabilitiesAsync(null);

        var carrier = Assert.Single(carriers);
        Assert.Equal(CarrierCode.PPL, carrier.CarrierCode);
        var service = Assert.Single(carrier.Services);
        Assert.Equal("HOME", service.Code);
        Assert.Equal("Home", service.Name);
        Assert.True(service.HomeDelivery);
        Assert.Equal(2, service.Countries.Count);
        Assert.True(service.Countries[CountryCode.CZ]);
        Assert.True(service.Countries[CountryCode.DE]);
        Assert.False(service.Countries.ContainsKey(CountryCode.US));
        Assert.Empty(service.Cod);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task CodAsyncConvertsMajorPricesToMinorUnits()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {
              "status": 200,
              "service_types": [{
                "service_type": "VMCZ",
                "countries": [{"country": " cz ", "currency": "czk", "max_price": 1499.95}]
              }]
            }
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var service = Assert.Single(await client.CodAsync(CarrierCode.ZASILKOVNA));

        var capability = Assert.Single(service.Countries);
        Assert.Equal(CountryCode.CZ, capability.Country);
        Assert.Equal(CurrencyCode.CZK, capability.Currency);
        Assert.Equal(149995, capability.MaxAmountMinor);
    }

    [Theory]
    [InlineData("1.001")]
    [InlineData("1e999999999")]
    [InlineData("-1")]
    public async Task CodAsyncRejectsUnsafePrices(string price)
    {
        var handler = new FakeHandler(_ => Json(
            $$"""
            {
              "status": 200,
              "service_types": [{
                "service_type": "VMCZ",
                "countries": [{"country": "CZ", "currency": "CZK", "max_price": {{price}}}]
              }]
            }
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.CodAsync(CarrierCode.ZASILKOVNA));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
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
}
