namespace Balikobot;

/// <summary>A provider label downloaded by <see cref="BalikobotClient.DownloadLabelAsync"/>.</summary>
/// <param name="Bytes">The label body.</param>
/// <param name="MediaType">The label media type, either "application/pdf" or "application/zpl".</param>
public sealed record DownloadedLabel(byte[] Bytes, string MediaType);
