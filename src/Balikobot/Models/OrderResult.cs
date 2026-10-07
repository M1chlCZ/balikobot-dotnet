namespace Balikobot;

/// <summary>The provider batch reference returned by ORDER.</summary>
public sealed record OrderResult
{
    /// <summary>Gets the provider batch identifier.</summary>
    public string OrderId { get; init; } = string.Empty;
}
