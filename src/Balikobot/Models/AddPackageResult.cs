namespace Balikobot;

/// <summary>The accepted package record returned by ADD.</summary>
public sealed record AddPackageResult
{
    /// <summary>Gets the Balíkobot package reference used by labels, ORDER and DROP.</summary>
    public string PackageId { get; init; } = string.Empty;

    /// <summary>Gets the carrier tracking number.</summary>
    public string CarrierId { get; init; } = string.Empty;

    /// <summary>Gets the provider label URL for this package.</summary>
    public string LabelUrl { get; init; } = string.Empty;
}
