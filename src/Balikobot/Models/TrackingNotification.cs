namespace Balikobot;

/// <summary>One package status change delivered to a Balíkobot tracking webhook. At least one of <see cref="PackageId"/>, <see cref="Eid"/> and <see cref="CarrierId"/> identifies the package; <see cref="StatusId"/> and <see cref="StatusText"/> describe the new provider status.</summary>
public sealed record TrackingNotification
{
    /// <summary>Gets the Balíkobot package reference. The value is empty when the notification identifies the package another way.</summary>
    public string PackageId { get; init; } = string.Empty;

    /// <summary>Gets the external package reference. The value is empty when the notification identifies the package another way.</summary>
    public string Eid { get; init; } = string.Empty;

    /// <summary>Gets the carrier tracking number. The value is empty when the notification identifies the package another way.</summary>
    public string CarrierId { get; init; } = string.Empty;

    /// <summary>Gets the raw provider status code, for example "1", "1.2" or "-1". The detailed status_id_v2 value wins when both are present.</summary>
    public string StatusId { get; init; } = string.Empty;

    /// <summary>Gets the provider status description.</summary>
    public string StatusText { get; init; } = string.Empty;
}
