using Balikobot.Codes;

namespace Balikobot;

/// <summary>Account information returned by the WHOAMI method.</summary>
public sealed record WhoAmI
{
    /// <summary>Gets the top-level provider status.</summary>
    public int Status { get; init; }

    /// <summary>Gets a value indicating whether the credentials belong to a live account. It is null when the provider omits the flag.</summary>
    public bool? LiveAccount { get; init; }

    /// <summary>Gets the carriers contracted by the account.</summary>
    public IReadOnlyList<WhoAmICarrier> Carriers { get; init; } = [];
}

/// <summary>One contracted carrier of the account.</summary>
public sealed record WhoAmICarrier
{
    /// <summary>Gets the carrier code used in request paths.</summary>
    public CarrierCode Slug { get; init; }

    /// <summary>Gets the carrier display name. It can be empty.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>Aggregates the discovered services of one contracted carrier.</summary>
public sealed record Carrier
{
    /// <summary>Gets the carrier code used in request paths.</summary>
    public CarrierCode CarrierCode { get; init; }

    /// <summary>Gets the activated services of the carrier.</summary>
    public IReadOnlyList<Service> Services { get; init; } = [];
}

/// <summary>Describes one activated carrier service.</summary>
public sealed record Service
{
    /// <summary>Gets the provider service code.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Gets the provider service name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets a value indicating home delivery support. Null means that the provider did not declare the flag.</summary>
    public bool? HomeDelivery { get; init; }

    /// <summary>Gets a value indicating box delivery support. Null means that the provider did not declare the flag.</summary>
    public bool? BoxDelivery { get; init; }

    /// <summary>Gets a value indicating pickup point delivery support. Null means that the provider did not declare the flag.</summary>
    public bool? PickupPointsDelivery { get; init; }

    /// <summary>Gets the destination country codes supported by the service. Combined discovery keeps only EU destinations.</summary>
    public Dictionary<CountryCode, bool> Countries { get; init; } = [];

    /// <summary>Gets the supported cash-on-delivery destinations. Combined discovery leaves it empty because it does not request the optional COD dictionary.</summary>
    public IReadOnlyList<CODCapability> Cod { get; init; } = [];
}

/// <summary>One cash-on-delivery destination of a service.</summary>
public sealed record CODCapability
{
    /// <summary>Gets the ISO 3166-1 alpha-2 destination country.</summary>
    public CountryCode Country { get; init; }

    /// <summary>Gets the three-letter currency code.</summary>
    public CurrencyCode Currency { get; init; }

    /// <summary>Gets the maximum cash-on-delivery amount in minor units, for example 149995 for 1499.95 CZK.</summary>
    public long MaxAmountMinor { get; init; }
}

/// <summary>The ACTIVATEDSERVICES answer of one carrier.</summary>
public sealed record ActivatedServices
{
    /// <summary>Gets the provider flag that marks active parcel shipping. It is null when the provider omits the flag.</summary>
    public bool? ActiveParcel { get; init; }

    /// <summary>Gets the activated services. The countries and COD fields are empty; use carrier capabilities for the combined discovery.</summary>
    public IReadOnlyList<Service> Services { get; init; } = [];
}

/// <summary>One entry of the COUNTRIES4SERVICE answer.</summary>
public sealed record ServiceCountries
{
    /// <summary>Gets the provider service code.</summary>
    public string ServiceType { get; init; } = string.Empty;

    /// <summary>Gets the destination country codes exactly as sent, with surrounding whitespace trimmed and letters upper-cased.</summary>
    public IReadOnlyList<CountryCode> Countries { get; init; } = [];
}

/// <summary>One entry of the COD4SERVICES answer.</summary>
public sealed record ServiceCOD
{
    /// <summary>Gets the provider service code.</summary>
    public string ServiceType { get; init; } = string.Empty;

    /// <summary>Gets the normalized cash-on-delivery destinations.</summary>
    public IReadOnlyList<CODCapability> Countries { get; init; } = [];
}
