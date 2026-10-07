# Balikobot

A .NET client for the Balíkobot shipping API v2. The client uses only the .NET
base class library. It covers branches, packages, labels, tracking, pickup
orders and account capabilities.

## Install

```sh
dotnet add package Balikobot
```

The package requires .NET 10 or later. The client accepts the `http` scheme
only for a loopback test server. Every other base URL must use `https`.

## Quick start

Create a client with your API user and API key. The client sends the
credentials as HTTP Basic authentication. Then add a package, get a label URL
and read the tracking status.

```csharp
using Balikobot;
using Balikobot.Codes;

using var client = new BalikobotClient(new BalikobotConfig
{
    User = "api-user",
    ApiKey = "api-key",
    Timeout = TimeSpan.FromSeconds(15),
});

var result = await client.AddPackageAsync(CarrierCode.PPL, new AddPackageRequest
{
    Eid = "order-2026-000123-S1",
    ServiceType = "1",
    RecName = "Example Recipient",
    RecStreet = "Example 1",
    RecCity = "Praha",
    RecZip = "11000",
    RecCountry = CountryCode.CZ,
    RecPhone = "+420777000000",
    WeightKg = 1.5,
    LengthCm = 30,
    WidthCm = 20,
    HeightCm = 10,
    Price = 1000,
    CodCurrency = CurrencyCode.CZK,
});

var labelUrl = await client.LabelsAsync(CarrierCode.PPL, result.PackageId);
Console.WriteLine(labelUrl);

var status = await client.TrackStatusAsync(CarrierCode.PPL, result.CarrierId);
Console.WriteLine(status.StatusText);
```

## Methods

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `BranchesAsync` | `GET /{carrier}/branches/...` | Lists the branches of a service and country |
| `AddPackageAsync` | `POST /{carrier}/add` | Creates one package. ADD is idempotent on `Eid` |
| `OverviewAsync` | `GET /{carrier}/overview` | Lists the packages that ORDER has not closed |
| `LabelsAsync` | `POST /{carrier}/labels` | Gets a fresh label URL for one package |
| `OrderViewLabelsAsync` | `GET /{carrier}/orderview/{order_id}` | Gets the label URL of a closed order |
| `DownloadLabelAsync` | `GET` the label URL | Downloads the label body |
| `TrackStatusAsync` | `POST /{carrier}/trackstatus` | Reads the tracking status of one package |
| `OrderBatchAsync` | `POST /{carrier}/order` | Hands one package to the carrier batch |
| `DropPackageAsync` | `POST /{carrier}/drop` | Removes one package before ORDER |
| `OrderPickupAsync` | `POST /{carrier}/orderpickup` | Books one physical collection |
| `WhoAmIAsync` | `GET /info/whoami` | Reads the account and carrier data |
| `ActivatedServicesAsync` | `GET /{carrier}/activatedservices` | Lists the activated services |
| `CountriesAsync` | `GET /{carrier}/countries4service` | Lists the destination countries |
| `CodAsync` | `GET /{carrier}/cod4services` | Lists the cash-on-delivery destinations |
| `CarrierCapabilitiesAsync` | `GET` the discovery endpoints | Discovers the contracted carriers and services |
| `ResolveBranchId` | none | Chooses the branch id or the branch zip for an ADD request |

## Codes

Carrier, currency and country values are typed, not plain strings. The
`Balikobot.Codes` namespace holds three types:

| Type | Format | Common constants |
| --- | --- | --- |
| `CarrierCode` | `^[a-z0-9]{2,32}$` | `PPL`, `DPD`, `DPDCZ`, `DPDSK`, `GEIS`, `GLS`, `INTIME`, `CP`, `CESKAPOSTA`, `BALIKOVNA`, `ZASILKOVNA`, `SP`, `ULOZENKA` |
| `CurrencyCode` | ISO 4217 `^[A-Z]{3}$` | `CZK`, `EUR`, `USD`, `GBP`, `PLN`, `HUF`, `RON`, `BGN`, `HRK`, `CHF`, `NOK`, `SEK`, `DKK` |
| `CountryCode` | ISO 3166-1 alpha-2 `^[A-Z]{2}$` | EU member states plus `GB`, `CH`, `NO`, `IS`, `LI`, `UA`, `RS`, `BA`, `ME`, `MK`, `AL`, `TR`, `US`, `CA` |

Every type has `Parse`, `TryParse` and `IsValid`. `Parse` and `TryParse` trim
whitespace and normalize the case. They accept any well-formed code, so custom
carriers, currencies and countries work:

```csharp
var custom = CarrierCode.Parse("MyCarrier99");
var result = await client.AddPackageAsync(custom, request);
```

The types keep the wire values compile-time safe. A call that expects a
`CarrierCode` rejects a bare string. A mistyped constant fails the build. The
JSON form stays a plain string, so the wire contract does not change.

ADD still accepts only `CurrencyCode.CZK` and `CurrencyCode.EUR` as
`CodCurrency`. The carriers require one of those two values. The client rejects
other well-formed currency codes before the request.

## Errors

The client reports every failure with a `BalikobotException`. The `Error`
property holds a `BalikobotError` value. Test the value with a `switch`
statement or an `if` statement.

| `BalikobotError` | Meaning | Action |
| --- | --- | --- |
| `InvalidRequest` | The arguments are not valid. The client sent no request. | Correct the input. Do not retry. |
| `Rejected` | The provider refused the data permanently. | Correct the data. Do not retry. |
| `Unavailable` | The provider is unavailable, or the request never left the client. | Retry later. |
| `NotFound` | The carrier has no tracking data yet. | Poll again later. |
| `Ambiguous` | A mutating call can have reached the provider. | Reconcile with `OverviewAsync`. Then retry. |
| `InvalidResponse` | The answer violates the protocol. | Inspect the provider. Do not retry blindly. |
| `Config` | The client configuration is not valid. | Correct the configuration. |
| `InvalidCode` | A typed code value is malformed. | Correct the code. |

A JSON endpoint answer can carry a `Retry-After` header. The `Unavailable`
exception then holds the delay in the `RetryAfter` property. The property is
`null` when the provider sent no header.

```csharp
catch (BalikobotException exception)
    when (exception.Error == BalikobotError.Unavailable && exception.RetryAfter is { } retryAfter)
{
    await Task.Delay(retryAfter);
}
```

The client never retries a request. The caller controls the retry policy.

## Response limits

The client reads every JSON body with a hard limit of 8 MiB. Set
`BalikobotConfig.MaxResponseBytes` to change the limit. Label downloads use a
fixed limit of 4 MiB. The client refuses redirects. It checks the response
`Content-Type` before it decodes the body.

## Custom HTTP client

Set `BalikobotConfig.HttpClient` to use a client that the caller owns. The
library never disposes an injected client. It cannot clone the instance, so the
caller must configure it before use:

- Set `AllowAutoRedirect` to `false` on the handler. An injected client must
  refuse redirects, because a redirect can send the credentials to another
  origin.
- Set `HttpClient.Timeout` on the client. `BalikobotConfig.Timeout` applies
  only to the client that the library creates.

The library uses the injected client as it is. The response limit and the label
checks stay active.

## Account mode

Set `BalikobotConfig.LiveAccount` to `true` or `false` to verify the account
before each mutating call. The client calls WHOAMI and compares the
`live_account` flag. A mismatch blocks the write before the client sends it. A
successful result stays valid for five minutes. A `null` value skips the check.

## Label hosts

The client accepts label URLs only from the Balíkobot label hosts, or from the
base URL origin for a loopback test server. The default allowlist accepts every
`https` URL on `balikobot.cz` or a subdomain. Set `BalikobotConfig.LabelHosts`
to replace the default allowlist with other hosts. A leading dot selects a
subdomain suffix match. It does not match the bare domain.

## Parity notes

The library follows the Go reference client with a few accepted differences:

- Cancellation propagates as `OperationCanceledException`. The library does not
  wrap it in a `BalikobotException`.
- JSON property matching is case-sensitive.
- The transport uses the platform defaults. It sets no connection attempt cap
  and no minimum TLS version.
- The test suite is minimal. The tests cover the mapping rules with local HTTP
  servers. They do not cover every provider answer.

## Releasing

The package publishes with NuGet Trusted Publishing. The repository stores no
API key.

One-time setup on nuget.org:

1. Sign in to nuget.org.
2. Open Trusted Publishing.
3. Add a policy with these values:
   - Package owner: your account
   - Repository owner: `M1chlCZ`
   - Repository: `balikobot-dotnet`
   - Workflow file: `publish.yml`
   - Environment: empty

One-time setup in GitHub:

1. Open the repository settings.
2. Select Secrets and variables, then Actions, then the Variables tab.
3. Add the repository variable `NUGET_USER` with your nuget.org username.

To publish a release:

1. Create a GitHub release with a tag, for example `v0.1.0`.
2. The `publish` workflow builds, tests, packs, and pushes the package.

You can also run the `publish` workflow manually from the Actions tab.

## Development

Run the checks from the repository root:

```sh
dotnet build -c Release -warnaserror
dotnet test -c Release
```

The tests use local HTTP servers. They use no real credentials and no external
network.

## License

MIT. See [LICENSE](LICENSE).
