using System.Net;
using System.Text;
using Balikobot.Codes;

namespace Balikobot.Tests;

public class BranchesTests
{
    private const string BaseUrl = "http://127.0.0.1:43123";

    [Fact]
    public async Task ArrayPayloadReturnsBranches()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {
              "status": 200,
              "branches": [
                {
                  "branch_id": "123", "type": "branch", "name": "PPL Pickup Praha",
                  "street": "Psí 1", "city": "Praha", "zip": "11000", "country": "CZ",
                  "lat": 50.0755, "lng": 14.4378
                },
                {"id": 456, "name": "PPL Pickup Brno", "street": "Veterinární 2", "city": "Brno", "zip": "60200"}
              ]
            }
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var branches = await client.BranchesAsync(CarrierCode.PPL, "1", CountryCode.CZ);

        Assert.Equal(2, branches.Count);
        var first = branches[0];
        Assert.Equal("123", first.Id);
        Assert.Equal("branch", first.Type);
        Assert.Equal("PPL Pickup Praha", first.Name);
        Assert.Equal("Psí 1", first.Street);
        Assert.Equal("Praha", first.City);
        Assert.Equal("11000", first.Zip);
        Assert.Equal("CZ", first.Country.Value);
        Assert.Equal(50.0755, first.Latitude!.Value);
        Assert.Equal(14.4378, first.Longitude!.Value);
        Assert.Equal("456", branches[1].Id);
        Assert.Null(branches[1].Latitude);
        Assert.Equal("/ppl/branches/service/1/country/CZ", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ObjectPayloadReturnsBranchesInNumericKeyOrder()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {
              "status": "200",
              "branches": {
                "456": {"id": "456", "name": "Pobočka B", "zip": "12000"},
                "1a": {"id": "non-numeric", "name": "Pobočka C", "zip": "13000"},
                "123": {"id": "123", "name": "Pobočka A", "zip": "11000"}
              }
            }
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var branches = await client.BranchesAsync(CarrierCode.PPL, "1", CountryCode.CZ);

        Assert.Equal(
            new[] { "123", "456", "non-numeric" },
            branches.Select(branch => branch.Id));
    }

    [Fact]
    public async Task ObjectPayloadSortsKeysThatExceedInt32AsNumbers()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {
              "status": 200,
              "branches": {
                "10000000000": {"id": "A2", "name": "Pobočka B", "zip": "12000"},
                "9999999999": {"id": "A1", "name": "Pobočka A", "zip": "11000"}
              }
            }
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var branches = await client.BranchesAsync(CarrierCode.PPL, "1", CountryCode.CZ);

        Assert.Equal(new[] { "A1", "A2" }, branches.Select(branch => branch.Id));
    }

    [Fact]
    public async Task NameFallsBackToZipAndTheCountryFilterDropsForeignBranches()
    {
        var handler = new FakeHandler(_ => Json(
            """
            {
              "status": 200,
              "branches": [
                {"id": "1", "street": "Psí 1", "city": "Praha", "zip": "11000", "country": "CZ"},
                {
                  "id": "2", "name": "Balíkovna Bratislava", "street": "Psí 2",
                  "city": "Bratislava", "zip": "81101", "country": "SK"
                }
              ]
            }
            """));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var branches = await client.BranchesAsync(CarrierCode.CP, "NP", CountryCode.CZ);

        var branch = Assert.Single(branches);
        Assert.Equal("1", branch.Id);
        Assert.Equal("11000", branch.Name);
        Assert.Equal("/cp/branches/service/NP/country/CZ", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ServerErrorThrowsUnavailable()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.BranchesAsync(CarrierCode.PPL, "1", CountryCode.CZ));

        Assert.Equal(BalikobotError.Unavailable, exception.Error);
        Assert.Null(exception.RetryAfter);
    }

    [Fact]
    public async Task MalformedStatusThrowsInvalidResponse()
    {
        var handler = new FakeHandler(_ => Json(
            """{"status":"OK","branches":[]}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<BalikobotException>(
            () => client.BranchesAsync(CarrierCode.PPL, "1", CountryCode.CZ));

        Assert.Equal(BalikobotError.InvalidResponse, exception.Error);
    }

    [Fact]
    public async Task NullBranchEntriesAreSkipped()
    {
        var handler = new FakeHandler(_ => Json(
            """{"status":200,"branches":[null,{"id":"1","name":"Pobočka A","zip":"11000"}]}"""));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        var branch = Assert.Single(await client.BranchesAsync(CarrierCode.PPL, "1", CountryCode.CZ));

        Assert.Equal("1", branch.Id);
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
