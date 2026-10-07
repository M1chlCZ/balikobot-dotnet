namespace Balikobot;

/// <summary>The latest provider tracking status of one package.</summary>
public sealed record TrackStatusResult
{
    /// <summary>Gets the raw provider status code, for example "1", "1.2" or "-1".</summary>
    public string StatusId { get; init; } = string.Empty;

    /// <summary>Gets the provider status description.</summary>
    public string StatusText { get; init; } = string.Empty;
}
