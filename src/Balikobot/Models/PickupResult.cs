namespace Balikobot;

/// <summary>The confirmed collection booking returned by ORDERPICKUP.</summary>
public sealed record PickupResult
{
    /// <summary>Gets the provider pickup reference. PPL returns it; DPD and DPDCZ leave it empty.</summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the provider confirmed the collection. DPD and DPDCZ always confirm on success.</summary>
    public bool Confirmed { get; init; }
}
