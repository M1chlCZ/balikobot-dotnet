namespace Balikobot;

/// <summary>Describes one physical collection booking for ORDERPICKUP.</summary>
public sealed record PickupRequest
{
    /// <summary>Gets the collection date in the canonical YYYY-MM-DD format.</summary>
    public string Date { get; init; } = string.Empty;

    /// <summary>Gets the total collection weight in kilograms. It must be positive and at most 100000. DPD and DPDCZ send it; PPL takes the weight from its carrier configuration.</summary>
    public double WeightKg { get; init; }

    /// <summary>Gets the number of packages. It must be between 1 and 10000. DPD and DPDCZ send it; PPL takes it from its carrier configuration.</summary>
    public int PackageCount { get; init; }

    /// <summary>Gets the optional collection note. It must contain at most 255 characters without line breaks. DPD and DPDCZ send it as "message"; PPL sends it as "note".</summary>
    public string Note { get; init; } = string.Empty;
}
