using System.Net;
using System.Text;
using Balikobot.Codes;

namespace Balikobot;

/// <summary>A Balíkobot API v2 client. Every method is safe for concurrent use except <see cref="Dispose"/>.</summary>
public sealed class BalikobotClient : IDisposable
{
    private const int UserLimit = 100;
    private const int ApiKeyLimit = 4096;
    private const int MaxResponseBytesLimit = 1 << 30;
    private static readonly char[] HostSeparators = ['/', '\\', '@', '?', '#'];

    private readonly bool _ownsHttpClient;
    private bool _disposed;

    /// <summary>Initializes a client from the configuration.</summary>
    /// <param name="config">The client configuration.</param>
    /// <exception cref="BalikobotException">The configuration is invalid.</exception>
    public BalikobotClient(BalikobotConfig config)
    {
        var user = config.User.Trim();
        if (user.Length == 0 || Encoding.UTF8.GetByteCount(user) > UserLimit)
        {
            throw new BalikobotException(
                BalikobotError.Config,
                "balikobot: invalid configuration: user is required and limited to 100 bytes");
        }

        if (config.ApiKey.Length == 0 || Encoding.UTF8.GetByteCount(config.ApiKey) > ApiKeyLimit)
        {
            throw new BalikobotException(
                BalikobotError.Config,
                "balikobot: invalid configuration: API key is required and limited to 4096 bytes");
        }

        if (config.Timeout < TimeSpan.Zero)
        {
            throw new BalikobotException(
                BalikobotError.Config,
                "balikobot: invalid configuration: timeout must not be negative");
        }

        if (config.MaxResponseBytes < 0 || config.MaxResponseBytes > MaxResponseBytesLimit)
        {
            throw new BalikobotException(
                BalikobotError.Config,
                "balikobot: invalid configuration: response limit must be between 0 and 1073741824 bytes");
        }

        var (baseUrl, origin, loopback) = Wire.ResolveBaseUrl(config.BaseUrl);
        var labelHosts = NormalizeLabelHosts(config.LabelHosts);
        var timeout = config.Timeout == TimeSpan.Zero ? BalikobotConfig.DefaultTimeout : config.Timeout;

        if (config.HttpClient is { } injected)
        {
            Transport = injected;
        }
        else
        {
            Transport = new HttpClient(
                new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.All,
                },
                disposeHandler: true)
            {
                Timeout = timeout,
            };
            _ownsHttpClient = true;
        }

        BaseUrl = baseUrl;
        Origin = origin;
        Loopback = loopback;
        MaxResponseBytes = config.MaxResponseBytes == 0
            ? BalikobotConfig.DefaultMaxResponseBytes
            : config.MaxResponseBytes;
        LabelHosts = labelHosts;
        LiveAccount = config.LiveAccount;
        Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + config.ApiKey));
    }

    internal HttpClient Transport { get; }

    internal string BaseUrl { get; }

    internal string Authorization { get; }

    internal string Origin { get; }

    internal bool Loopback { get; }

    internal int MaxResponseBytes { get; }

    internal IReadOnlyList<string> LabelHosts { get; }

    internal bool? LiveAccount { get; }

    /// <summary>Releases the owned HTTP client. A caller-owned client is left untouched.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsHttpClient)
        {
            Transport.Dispose();
        }
    }

    /// <summary>Gets the branches of one carrier service in one country.</summary>
    /// <param name="carrier">The carrier code.</param>
    /// <param name="service">The carrier service code, 1 to 16 ASCII letters or digits.</param>
    /// <param name="country">The country code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The branches offered by the carrier, filtered to the requested country when the route needs it.</returns>
    /// <exception cref="BalikobotException">
    /// The request is invalid (<see cref="BalikobotError.InvalidRequest"/>), the provider is
    /// temporarily unavailable (<see cref="BalikobotError.Unavailable"/>), or the provider answer
    /// violates the protocol (<see cref="BalikobotError.InvalidResponse"/>).
    /// </exception>
    public async Task<IReadOnlyList<Branch>> BranchesAsync(
        CarrierCode carrier,
        string service,
        CountryCode country,
        CancellationToken cancellationToken = default)
    {
        if (carrier.Value is not { } carrierCode ||
            !CarrierCode.IsValid(carrierCode) ||
            service is null ||
            !IsValidService(service) ||
            country.Value is not { } countryCode ||
            !CountryCode.IsValid(countryCode))
        {
            throw new BalikobotException(BalikobotError.InvalidRequest, "balikobot: invalid request");
        }

        var (path, filterCountry) = BranchWire.BranchesPath(carrier, service, country);

        RawResponse response;
        try
        {
            response = await Wire.RequestAsync(this, HttpMethod.Get, path, null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RequestFailureException)
        {
            throw new BalikobotException(BalikobotError.Unavailable, "balikobot: temporarily unavailable");
        }

        if (response.Status == 429 || response.Status >= 500)
        {
            throw new BalikobotException(BalikobotError.Unavailable, "balikobot: temporarily unavailable");
        }

        if (response.Status != 200 ||
            !Wire.IsJson(response) ||
            !BranchWire.TryDecode(response.Body, out var status, out var decoded))
        {
            throw new BalikobotException(BalikobotError.InvalidResponse, "balikobot: invalid provider response");
        }

        if (status is 426 or 503)
        {
            throw new BalikobotException(BalikobotError.Unavailable, "balikobot: temporarily unavailable");
        }

        if (status != 200)
        {
            throw new BalikobotException(BalikobotError.InvalidResponse, "balikobot: invalid provider response");
        }

        var branches = new List<Branch>(decoded.Count);
        foreach (var branch in decoded)
        {
            if (filterCountry && branch.Country.Value.Length > 0 && branch.Country != country)
            {
                continue;
            }

            branches.Add(branch);
        }

        return branches;
    }

    /// <summary>Derives the branch id that ADD expects from a stored branch of a carrier. Česká pošta and Slovenská pošta use the branch ZIP without spaces, the Uloženka CP_NP service does the same, PPL strips the KM prefix, and every other carrier uses the stored branch id unchanged.</summary>
    /// <param name="carrier">The carrier code.</param>
    /// <param name="service">The carrier service code.</param>
    /// <param name="branchId">The stored branch identifier.</param>
    /// <param name="branchZip">The stored branch postal code.</param>
    /// <returns>The branch id for the ADD request.</returns>
    public static string ResolveBranchId(CarrierCode carrier, string service, string branchId, string branchZip)
    {
        var id = branchId ?? string.Empty;
        var zip = branchZip ?? string.Empty;
        if (carrier == CarrierCode.CP || carrier == CarrierCode.SP)
        {
            return zip.Replace(" ", string.Empty, StringComparison.Ordinal);
        }

        if (carrier == CarrierCode.ULOZENKA)
        {
            return service == "CP_NP"
                ? zip.Replace(" ", string.Empty, StringComparison.Ordinal)
                : id;
        }

        if (carrier == CarrierCode.PPL)
        {
            return id.StartsWith("KM", StringComparison.Ordinal) ? id[2..] : id;
        }

        return id;
    }

    /// <summary>Adds one package with the ADD method. ADD is idempotent on the external reference: a repeated request with an already stored EID returns status 208 together with the original record.</summary>
    /// <param name="carrier">The carrier code.</param>
    /// <param name="request">The package to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accepted package record.</returns>
    /// <exception cref="BalikobotException">
    /// The request is invalid (<see cref="BalikobotError.InvalidRequest"/>), the provider permanently
    /// rejected the data (<see cref="BalikobotError.Rejected"/>), the provider is temporarily unavailable
    /// (<see cref="BalikobotError.Unavailable"/>), the provider answer violates the protocol
    /// (<see cref="BalikobotError.InvalidResponse"/>), or the request may have reached the provider
    /// (<see cref="BalikobotError.Ambiguous"/>).
    /// </exception>
    public async Task<AddPackageResult> AddPackageAsync(
        CarrierCode carrier,
        AddPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (carrier.Value is not { } carrierCode ||
            !CarrierCode.IsValid(carrierCode) ||
            !ShipmentWire.ValidAddRequest(request))
        {
            throw ShipmentWire.Error(BalikobotError.InvalidRequest);
        }

        RawResponse response;
        try
        {
            response = await Wire.RequestAsync(
                this,
                HttpMethod.Post,
                "/" + carrierCode + "/add",
                new { packages = new[] { request } },
                cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailureException exception)
        {
            throw ShipmentWire.Error(exception.Kind == RequestFailureKind.ConnectionRefusedOrDns
                ? BalikobotError.Unavailable
                : BalikobotError.Ambiguous);
        }

        if (response.Status == 429)
        {
            throw ShipmentWire.Unavailable(Wire.RetryAfter(response));
        }

        if (response.Status >= 500)
        {
            throw ShipmentWire.Error(BalikobotError.Unavailable);
        }

        if (response.Status != 200)
        {
            throw ShipmentWire.Error(response.Status is >= 200 and < 300
                ? BalikobotError.Ambiguous
                : response.Status is >= 400 and < 500
                    ? BalikobotError.Rejected
                    : BalikobotError.InvalidResponse);
        }

        if (!Wire.IsJson(response) ||
            !ShipmentWire.TryDecodeAdd(response.Body, out var status, out var packages) ||
            status is null)
        {
            throw ShipmentWire.Error(BalikobotError.Ambiguous);
        }

        if (ShipmentWire.TopLevelStatusError(status.Value) is { } topError)
        {
            throw ShipmentWire.Error(topError == BalikobotError.InvalidResponse
                ? BalikobotError.Ambiguous
                : topError);
        }

        if (packages.Count != 1 || packages[0].Eid != request.Eid)
        {
            throw ShipmentWire.Error(BalikobotError.Ambiguous);
        }

        var entry = packages[0];
        if (entry.Status is null)
        {
            throw ShipmentWire.Error(BalikobotError.Ambiguous);
        }

        switch (entry.Status.Value)
        {
            case 200 or 208:
                if (entry.PackageId is null ||
                    entry.CarrierId.Length == 0 ||
                    !ShipmentWire.ValidField(entry.CarrierId, ShipmentWire.IdentifierLimit) ||
                    !Wire.ValidLabelUrl(this, entry.LabelUrl))
                {
                    throw ShipmentWire.Error(BalikobotError.Ambiguous);
                }

                return new AddPackageResult
                {
                    PackageId = entry.PackageId,
                    CarrierId = entry.CarrierId,
                    LabelUrl = entry.LabelUrl,
                };
            case 426 or 503:
                throw ShipmentWire.Error(BalikobotError.Unavailable);
            case 400 or 403 or 404 or 405 or 406 or 409 or 413 or 423 or 501:
                throw ShipmentWire.Error(BalikobotError.Rejected);
            default:
                throw ShipmentWire.Error(BalikobotError.Ambiguous);
        }
    }

    /// <summary>Lists the packages of a carrier that have not been closed by ORDER yet.</summary>
    /// <param name="carrier">The carrier code.</param>
    /// <param name="matchEid">The external reference whose integrity is required for reconciliation. A malformed entry with a different EID is skipped, while a malformed matching entry fails the call.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The open packages of the carrier.</returns>
    /// <exception cref="BalikobotException">
    /// The request is invalid (<see cref="BalikobotError.InvalidRequest"/>), the provider permanently
    /// rejected the request (<see cref="BalikobotError.Rejected"/>), the provider is temporarily
    /// unavailable (<see cref="BalikobotError.Unavailable"/>), the provider answer violates the protocol
    /// (<see cref="BalikobotError.InvalidResponse"/>), or the request may have reached the provider
    /// (<see cref="BalikobotError.Ambiguous"/>).
    /// </exception>
    public async Task<IReadOnlyList<OverviewPackage>> OverviewAsync(
        CarrierCode carrier,
        string matchEid,
        CancellationToken cancellationToken = default)
    {
        if (carrier.Value is not { } carrierCode || !CarrierCode.IsValid(carrierCode))
        {
            throw ShipmentWire.Error(BalikobotError.InvalidRequest);
        }

        RawResponse response;
        try
        {
            response = await Wire.RequestAsync(
                this,
                HttpMethod.Get,
                "/" + carrierCode + "/overview",
                null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailureException exception)
        {
            throw ShipmentWire.Error(exception.Kind == RequestFailureKind.ConnectionRefusedOrDns
                ? BalikobotError.Unavailable
                : BalikobotError.Ambiguous);
        }

        if (response.Status == 429)
        {
            throw ShipmentWire.Unavailable(Wire.RetryAfter(response));
        }

        if (response.Status >= 500)
        {
            throw ShipmentWire.Error(BalikobotError.Unavailable);
        }

        if (response.Status != 200 ||
            !Wire.IsJson(response) ||
            !ShipmentWire.TryDecodeOverview(response.Body, out var status, out var packages))
        {
            throw ShipmentWire.Error(response.Status is >= 400 and < 500
                ? BalikobotError.Rejected
                : BalikobotError.InvalidResponse);
        }

        if (status is { } value && ShipmentWire.TopLevelStatusError(value) is { } topError)
        {
            throw ShipmentWire.Error(topError);
        }

        var result = new List<OverviewPackage>(packages.Count);
        foreach (var entry in packages)
        {
            if (entry.Eid.Length > 0 &&
                entry.PackageId is not null &&
                entry.CarrierId.Length > 0 &&
                ShipmentWire.ValidField(entry.CarrierId, ShipmentWire.IdentifierLimit) &&
                Wire.ValidLabelUrl(this, entry.LabelUrl))
            {
                result.Add(new OverviewPackage
                {
                    Eid = entry.Eid,
                    PackageId = entry.PackageId,
                    CarrierId = entry.CarrierId,
                    LabelUrl = entry.LabelUrl,
                });
                continue;
            }

            if (entry.Eid == matchEid)
            {
                throw ShipmentWire.Error(BalikobotError.InvalidResponse);
            }
        }

        return result;
    }

    /// <summary>Asks for a fresh aggregate label URL of one package that has not entered ORDER yet.</summary>
    /// <param name="carrier">The carrier code.</param>
    /// <param name="packageId">The Balíkobot package reference.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provider label URL.</returns>
    /// <exception cref="BalikobotException">
    /// The request is invalid (<see cref="BalikobotError.InvalidRequest"/>), the provider permanently
    /// rejected the request (<see cref="BalikobotError.Rejected"/>), the provider is temporarily
    /// unavailable (<see cref="BalikobotError.Unavailable"/>), or the provider answer violates the
    /// protocol (<see cref="BalikobotError.InvalidResponse"/>).
    /// </exception>
    public async Task<string> LabelsAsync(
        CarrierCode carrier,
        string packageId,
        CancellationToken cancellationToken = default)
    {
        if (carrier.Value is not { } carrierCode ||
            !CarrierCode.IsValid(carrierCode) ||
            packageId is null ||
            !ShipmentWire.ValidPackageId(packageId))
        {
            throw ShipmentWire.Error(BalikobotError.InvalidRequest);
        }

        RawResponse response;
        try
        {
            response = await Wire.RequestAsync(
                this,
                HttpMethod.Post,
                "/" + carrierCode + "/labels",
                new { package_ids = new[] { packageId } },
                cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailureException)
        {
            throw ShipmentWire.Error(BalikobotError.Unavailable);
        }

        if (ShipmentWire.LabelLookupStatus(response) is { } statusError)
        {
            throw statusError;
        }

        if (!ShipmentWire.TryDecodeLabels(response.Body, out var status, out var labelUrl) || status is null)
        {
            throw ShipmentWire.Error(BalikobotError.InvalidResponse);
        }

        if (ShipmentWire.TopLevelStatusError(status.Value) is { } topError)
        {
            throw ShipmentWire.Error(topError);
        }

        if (!Wire.ValidLabelUrl(this, labelUrl))
        {
            throw ShipmentWire.Error(BalikobotError.InvalidResponse);
        }

        return labelUrl;
    }

    /// <summary>Retrieves the label URL of an already closed ORDER. The URL is accepted only when the response confirms both the requested order id and the membership of the requested package id.</summary>
    /// <param name="carrier">The carrier code.</param>
    /// <param name="orderId">The ORDER reference.</param>
    /// <param name="packageId">The Balíkobot package reference.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provider label URL.</returns>
    /// <exception cref="BalikobotException">
    /// The request is invalid (<see cref="BalikobotError.InvalidRequest"/>), the provider permanently
    /// rejected the request (<see cref="BalikobotError.Rejected"/>), the provider is temporarily
    /// unavailable (<see cref="BalikobotError.Unavailable"/>), or the provider answer violates the
    /// protocol (<see cref="BalikobotError.InvalidResponse"/>).
    /// </exception>
    public async Task<string> OrderViewLabelsAsync(
        CarrierCode carrier,
        string orderId,
        string packageId,
        CancellationToken cancellationToken = default)
    {
        if (carrier.Value is not { } carrierCode ||
            !CarrierCode.IsValid(carrierCode) ||
            orderId is null ||
            !ShipmentWire.ValidPackageId(orderId) ||
            packageId is null ||
            !ShipmentWire.ValidPackageId(packageId))
        {
            throw ShipmentWire.Error(BalikobotError.InvalidRequest);
        }

        RawResponse response;
        try
        {
            response = await Wire.RequestAsync(
                this,
                HttpMethod.Get,
                "/" + carrierCode + "/orderview/" + Uri.EscapeDataString(orderId),
                null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailureException)
        {
            throw ShipmentWire.Error(BalikobotError.Unavailable);
        }

        if (ShipmentWire.LabelLookupStatus(response) is { } statusError)
        {
            throw statusError;
        }

        if (!ShipmentWire.TryDecodeOrderView(
                response.Body,
                out var status,
                out var responseOrderId,
                out var packageIds,
                out var labelUrl))
        {
            throw ShipmentWire.Error(BalikobotError.InvalidResponse);
        }

        if (status is { } value && ShipmentWire.TopLevelStatusError(value) is { } topError)
        {
            throw ShipmentWire.Error(topError);
        }

        if (responseOrderId != orderId ||
            !packageIds.Contains(packageId, StringComparer.Ordinal) ||
            !Wire.ValidLabelUrl(this, labelUrl))
        {
            throw ShipmentWire.Error(BalikobotError.InvalidResponse);
        }

        return labelUrl;
    }

    /// <summary>Fetches a provider label URL server to server. The body is read once with a hard byte limit and validated against the declared label media type.</summary>
    /// <param name="labelUrl">The provider label URL.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The downloaded label.</returns>
    /// <exception cref="BalikobotException">
    /// The request is invalid (<see cref="BalikobotError.InvalidRequest"/>), the provider permanently
    /// rejected the request (<see cref="BalikobotError.Rejected"/>), the provider is temporarily
    /// unavailable (<see cref="BalikobotError.Unavailable"/>), or the provider answer violates the
    /// protocol (<see cref="BalikobotError.InvalidResponse"/>).
    /// </exception>
    public async Task<DownloadedLabel> DownloadLabelAsync(
        string labelUrl,
        CancellationToken cancellationToken = default)
    {
        if (labelUrl is null || !Wire.ValidLabelUrl(this, labelUrl))
        {
            throw ShipmentWire.Error(BalikobotError.InvalidRequest);
        }

        return await ShipmentWire.DownloadLabelAsync(this, labelUrl, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets the latest provider tracking status of one package. The call is read-only, so transport failures are safe to retry.</summary>
    /// <param name="carrier">The carrier code.</param>
    /// <param name="carrierId">The carrier tracking number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The raw provider status.</returns>
    /// <exception cref="BalikobotException">
    /// The request is invalid (<see cref="BalikobotError.InvalidRequest"/>), the provider has no tracking
    /// data yet (<see cref="BalikobotError.NotFound"/>), the provider permanently rejected the request
    /// (<see cref="BalikobotError.Rejected"/>), the provider is temporarily unavailable
    /// (<see cref="BalikobotError.Unavailable"/>), or the provider answer violates the protocol
    /// (<see cref="BalikobotError.InvalidResponse"/>).
    /// </exception>
    public async Task<TrackStatusResult> TrackStatusAsync(
        CarrierCode carrier,
        string carrierId,
        CancellationToken cancellationToken = default)
    {
        if (carrier.Value is not { } carrierCode ||
            !CarrierCode.IsValid(carrierCode) ||
            carrierId is null ||
            !ShipmentWire.ValidPackageId(carrierId))
        {
            throw ShipmentWire.Error(BalikobotError.InvalidRequest);
        }

        RawResponse response;
        try
        {
            response = await Wire.RequestAsync(
                this,
                HttpMethod.Post,
                "/" + carrierCode + "/trackstatus",
                new { carrier_ids = new[] { carrierId } },
                cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailureException)
        {
            throw ShipmentWire.Error(BalikobotError.Unavailable);
        }

        if (TrackingWire.TrackHttpStatus(response) is { } statusError)
        {
            throw statusError;
        }

        if (!TrackingWire.TryDecodeTrackStatus(response.Body, out var status, out var packages))
        {
            throw ShipmentWire.Error(BalikobotError.InvalidResponse);
        }

        if (status is { } topStatus)
        {
            switch (topStatus)
            {
                case 200:
                    break;
                case 426 or 503:
                    throw ShipmentWire.Error(BalikobotError.Unavailable);
                case 404:
                    throw ShipmentWire.Error(BalikobotError.NotFound);
                default:
                    throw ShipmentWire.Error(BalikobotError.InvalidResponse);
            }
        }

        if (packages.Count != 1 || packages[0].CarrierId != carrierId)
        {
            throw ShipmentWire.Error(BalikobotError.InvalidResponse);
        }

        var entry = packages[0];
        if (entry.Status is null && (status is null || entry.Name.Length == 0))
        {
            throw ShipmentWire.Error(BalikobotError.InvalidResponse);
        }

        var effectiveStatus = entry.Status;
        if (effectiveStatus is null)
        {
            effectiveStatus = status;
        }

        switch (effectiveStatus)
        {
            case 200:
                var id = entry.StatusIdV2.Set ? entry.StatusIdV2 : entry.StatusId;
                var description = entry.Name.Length > 0 ? entry.Name : entry.StatusText;
                if (!id.Set || description.Length == 0 ||
                    !ShipmentWire.ValidField(description, TrackingWire.DescriptionLimit))
                {
                    throw ShipmentWire.Error(BalikobotError.InvalidResponse);
                }

                return new TrackStatusResult { StatusId = id.Raw, StatusText = description };
            case 404:
                throw ShipmentWire.Error(BalikobotError.NotFound);
            case 426 or 503:
                throw ShipmentWire.Error(BalikobotError.Unavailable);
            case 400 or 403 or 405 or 406 or 409 or 413 or 423:
                throw ShipmentWire.Error(BalikobotError.Rejected);
            default:
                throw ShipmentWire.Error(BalikobotError.InvalidResponse);
        }
    }

    /// <summary>Hands one package over to the carrier batch with the ORDER method. ORDER is idempotent on package ids: a repeated closure of the same dataset returns status 208 with the original order id, so an idempotent retry after an ambiguous answer replays the original record instead of closing the package twice.</summary>
    /// <param name="carrier">The carrier code.</param>
    /// <param name="packageId">The Balíkobot package reference.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provider batch reference.</returns>
    /// <exception cref="BalikobotException">
    /// The request is invalid (<see cref="BalikobotError.InvalidRequest"/>), the provider permanently
    /// rejected the request (<see cref="BalikobotError.Rejected"/>), the provider is temporarily
    /// unavailable (<see cref="BalikobotError.Unavailable"/>), or the request may have reached the
    /// provider (<see cref="BalikobotError.Ambiguous"/>).
    /// </exception>
    public async Task<OrderResult> OrderBatchAsync(
        CarrierCode carrier,
        string packageId,
        CancellationToken cancellationToken = default)
    {
        if (carrier.Value is not { } carrierCode ||
            !CarrierCode.IsValid(carrierCode) ||
            packageId is null ||
            !ShipmentWire.ValidPackageId(packageId))
        {
            throw ShipmentWire.Error(BalikobotError.InvalidRequest);
        }

        RawResponse response;
        try
        {
            response = await Wire.RequestAsync(
                this,
                HttpMethod.Post,
                "/" + carrierCode + "/order",
                new { package_ids = new[] { packageId } },
                cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailureException exception)
        {
            throw ShipmentWire.Error(exception.Kind == RequestFailureKind.ConnectionRefusedOrDns
                ? BalikobotError.Unavailable
                : BalikobotError.Ambiguous);
        }

        if (response.Status == 429)
        {
            throw ShipmentWire.Unavailable(Wire.RetryAfter(response));
        }

        if (response.Status >= 500)
        {
            throw ShipmentWire.Error(BalikobotError.Unavailable);
        }

        if (response.Status != 200 || !Wire.IsJson(response))
        {
            throw ShipmentWire.Error(response.Status is >= 200 and < 300
                ? BalikobotError.Ambiguous
                : response.Status is >= 400 and < 500
                    ? BalikobotError.Rejected
                    : BalikobotError.InvalidResponse);
        }

        if (!TrackingWire.TryDecodeOrder(response.Body, out var status, out var orderId) || status is null)
        {
            throw ShipmentWire.Error(BalikobotError.Ambiguous);
        }

        switch (status.Value)
        {
            case 200 or 208:
                if (!ShipmentWire.ValidPackageId(orderId))
                {
                    throw ShipmentWire.Error(BalikobotError.Ambiguous);
                }

                return new OrderResult { OrderId = orderId };
            case 426 or 503:
                throw ShipmentWire.Error(BalikobotError.Unavailable);
            case 400 or 402 or 403 or 404 or 405 or 406 or 409 or 413 or 423:
                throw ShipmentWire.Error(BalikobotError.Rejected);
            default:
                throw ShipmentWire.Error(BalikobotError.Ambiguous);
        }
    }

    /// <summary>Drops one package that has not entered ORDER with the DROP method. A body status 404 means the package is already gone and the call succeeds. An ambiguous DROP answer must be reconciled through OVERVIEW before any retry.</summary>
    /// <param name="carrier">The carrier code.</param>
    /// <param name="packageId">The Balíkobot package reference.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="BalikobotException">
    /// The request is invalid (<see cref="BalikobotError.InvalidRequest"/>), the provider permanently
    /// rejected the request (<see cref="BalikobotError.Rejected"/>), the provider is temporarily
    /// unavailable (<see cref="BalikobotError.Unavailable"/>), or the request may have reached the
    /// provider (<see cref="BalikobotError.Ambiguous"/>).
    /// </exception>
    public async Task DropPackageAsync(
        CarrierCode carrier,
        string packageId,
        CancellationToken cancellationToken = default)
    {
        if (carrier.Value is not { } carrierCode ||
            !CarrierCode.IsValid(carrierCode) ||
            packageId is null ||
            !ShipmentWire.ValidPackageId(packageId))
        {
            throw ShipmentWire.Error(BalikobotError.InvalidRequest);
        }

        RawResponse response;
        try
        {
            response = await Wire.RequestAsync(
                this,
                HttpMethod.Post,
                "/" + carrierCode + "/drop",
                new { package_ids = new[] { packageId } },
                cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailureException exception)
        {
            throw ShipmentWire.Error(exception.Kind == RequestFailureKind.ConnectionRefusedOrDns
                ? BalikobotError.Unavailable
                : BalikobotError.Ambiguous);
        }

        if (response.Status == 429)
        {
            throw ShipmentWire.Unavailable(Wire.RetryAfter(response));
        }

        if (response.Status >= 500)
        {
            throw ShipmentWire.Error(BalikobotError.Unavailable);
        }

        if (response.Status != 200 || !Wire.IsJson(response))
        {
            throw ShipmentWire.Error(response.Status is >= 200 and < 300
                ? BalikobotError.Ambiguous
                : response.Status is >= 400 and < 500
                    ? BalikobotError.Rejected
                    : BalikobotError.InvalidResponse);
        }

        if (!TrackingWire.TryDecodeDrop(response.Body, out var status) || status is null)
        {
            throw ShipmentWire.Error(BalikobotError.Ambiguous);
        }

        switch (status.Value)
        {
            case 200 or 404:
                return;
            case 426 or 503:
                throw ShipmentWire.Error(BalikobotError.Unavailable);
            case 400 or 402 or 403 or 405 or 406 or 409 or 413 or 423:
                throw ShipmentWire.Error(BalikobotError.Rejected);
            default:
                throw ShipmentWire.Error(BalikobotError.Ambiguous);
        }
    }

    /// <summary>Books one physical collection with the ORDERPICKUP method, separately from the shipment data handover performed by ORDER. The call performs exactly one HTTP attempt. DPD and DPDCZ take the collection address from the carrier configuration; PPL also defaults its contact information to that configuration.</summary>
    /// <param name="carrier">The carrier code: DPD, DPDCZ or PPL.</param>
    /// <param name="request">The collection booking.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The confirmed collection booking.</returns>
    /// <exception cref="BalikobotException">
    /// The carrier or the data was rejected locally (<see cref="BalikobotError.Rejected"/>), the provider
    /// permanently rejected the booking (<see cref="BalikobotError.Rejected"/>), or the request may have
    /// reached the provider (<see cref="BalikobotError.Ambiguous"/>).
    /// </exception>
    public async Task<PickupResult> OrderPickupAsync(
        CarrierCode carrier,
        PickupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!PickupWire.ValidPickupRequest(carrier, request))
        {
            throw ShipmentWire.Error(BalikobotError.Rejected);
        }

        RawResponse response;
        try
        {
            response = await Wire.RequestAsync(
                this,
                HttpMethod.Post,
                "/" + carrier.Value + "/orderpickup",
                PickupWire.RequestBody(carrier, request),
                cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailureException)
        {
            throw ShipmentWire.Error(BalikobotError.Ambiguous);
        }

        if (response.Status != 200)
        {
            throw PickupWire.PickupStatusError(response.Status);
        }

        if (!Wire.IsJson(response) ||
            !PickupWire.TryDecode(response.Body, out var status, out var providerId, out var confirmed) ||
            status is null)
        {
            throw ShipmentWire.Error(BalikobotError.Ambiguous);
        }

        if (status.Value != 200)
        {
            throw PickupWire.PickupStatusError(status.Value);
        }

        if (carrier != CarrierCode.PPL)
        {
            return new PickupResult { ProviderId = string.Empty, Confirmed = true };
        }

        if (confirmed is null || !ShipmentWire.ValidPackageId(providerId))
        {
            throw ShipmentWire.Error(BalikobotError.Ambiguous);
        }

        return new PickupResult { ProviderId = providerId, Confirmed = confirmed.Value };
    }

    private static bool IsValidService(string service)
    {
        if (service.Length is < 1 or > 16)
        {
            return false;
        }

        foreach (var character in service)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<string> NormalizeLabelHosts(IReadOnlyList<string> hosts)
    {
        var normalized = new string[hosts.Count];
        for (var index = 0; index < hosts.Count; index++)
        {
            var host = hosts[index].Trim().ToLowerInvariant();
            if (host.Length == 0 || host.IndexOfAny(HostSeparators) >= 0)
            {
                throw new BalikobotException(
                    BalikobotError.Config,
                    "balikobot: invalid configuration: label hosts must be host names");
            }

            normalized[index] = host;
        }

        return normalized;
    }
}
