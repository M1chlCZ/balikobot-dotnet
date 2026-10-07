namespace Balikobot;

/// <summary>Configures a <see cref="BalikobotClient"/>.</summary>
public sealed class BalikobotConfig
{
    /// <summary>The production Balíkobot API v2 endpoint.</summary>
    public const string DefaultBaseUrl = "https://apiv2.balikobot.cz";

    /// <summary>The whole-request timeout used when <see cref="Timeout"/> is zero.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The JSON response size limit used when <see cref="MaxResponseBytes"/> is zero.</summary>
    public const int DefaultMaxResponseBytes = 8 * 1024 * 1024;

    /// <summary>Gets the API root. Defaults to <see cref="DefaultBaseUrl"/>.</summary>
    public string BaseUrl { get; init; } = DefaultBaseUrl;

    /// <summary>Gets the API user. It must not be empty and is limited to 100 bytes.</summary>
    public string User { get; init; } = string.Empty;

    /// <summary>Gets the API key used as the HTTP Basic password. It must not be empty and is limited to 4096 bytes.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Gets the whole-request timeout. Zero selects <see cref="DefaultTimeout"/>.</summary>
    public TimeSpan Timeout { get; init; } = DefaultTimeout;

    /// <summary>Gets the hard byte limit for a JSON response body. Zero selects <see cref="DefaultMaxResponseBytes"/>.</summary>
    public int MaxResponseBytes { get; init; } = DefaultMaxResponseBytes;

    /// <summary>Gets the optional label host allowlist. A leading dot selects a suffix match.</summary>
    public IReadOnlyList<string> LabelHosts { get; init; } = Array.Empty<string>();

    /// <summary>Gets the optional account-mode expectation for mutating calls.</summary>
    public bool? LiveAccount { get; init; }

    /// <summary>Gets the optional caller-owned HTTP client. The client never disposes it.</summary>
    public HttpClient? HttpClient { get; init; }
}
