namespace Balikobot;

/// <summary>A provider label downloaded by <see cref="BalikobotClient.DownloadLabelAsync"/> or <see cref="BalikobotClient.DownloadBatchLabelsAsync"/>.</summary>
/// <param name="Bytes">The label body.</param>
/// <param name="MediaType">The label media type: "application/pdf" or "application/zpl" for a single label, "application/zip" for a batch archive.</param>
public sealed record DownloadedLabel(byte[] Bytes, string MediaType);
