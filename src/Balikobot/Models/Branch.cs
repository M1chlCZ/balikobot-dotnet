using Balikobot.Codes;

namespace Balikobot;

/// <summary>Describes one carrier branch or pickup point.</summary>
public sealed record Branch
{
    /// <summary>Gets the branch identifier used by the carrier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the provider branch type, for example "branch" or "box".</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets the display name. It falls back to <see cref="Zip"/> when the provider sends no name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the street part of the address.</summary>
    public string Street { get; init; } = string.Empty;

    /// <summary>Gets the city part of the address.</summary>
    public string City { get; init; } = string.Empty;

    /// <summary>Gets the postal code.</summary>
    public string Zip { get; init; } = string.Empty;

    /// <summary>Gets the ISO 3166-1 alpha-2 country code. It can be empty when the provider omits it for a domestic branch.</summary>
    public CountryCode Country { get; init; }

    /// <summary>Gets the GPS latitude when the provider sent a valid pair.</summary>
    public double? Latitude { get; init; }

    /// <summary>Gets the GPS longitude when the provider sent a valid pair.</summary>
    public double? Longitude { get; init; }
}
