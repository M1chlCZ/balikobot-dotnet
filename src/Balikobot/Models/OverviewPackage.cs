namespace Balikobot;

/// <summary>Describes one open package entry returned by OVERVIEW.</summary>
public sealed record OverviewPackage
{
    /// <summary>Gets the external package reference stored by the provider.</summary>
    public string Eid { get; init; } = string.Empty;

    /// <summary>Gets the Balíkobot package reference.</summary>
    public string PackageId { get; init; } = string.Empty;

    /// <summary>Gets the carrier tracking number.</summary>
    public string CarrierId { get; init; } = string.Empty;

    /// <summary>Gets the provider label URL for this package.</summary>
    public string LabelUrl { get; init; } = string.Empty;
}
