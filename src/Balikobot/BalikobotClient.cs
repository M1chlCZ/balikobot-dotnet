using System.Net;
using System.Text;

namespace Balikobot;

/// <summary>A Balíkobot API v2 client. Every method is safe for concurrent use except <see cref="Dispose"/>.</summary>
public sealed class BalikobotClient : IDisposable
{
    private const int UserLimit = 100;
    private const int ApiKeyLimit = 4096;
    private const int MaxResponseBytesLimit = 1 << 30;
    private static readonly char[] HostSeparators = ['/', '\\', '@', '?', '#'];

    private readonly bool _ownsHttpClient;
    private bool _disposed;

    /// <summary>Initializes a client from the configuration.</summary>
    /// <param name="config">The client configuration.</param>
    /// <exception cref="BalikobotException">The configuration is invalid.</exception>
    public BalikobotClient(BalikobotConfig config)
    {
        var user = config.User.Trim();
        if (user.Length == 0 || Encoding.UTF8.GetByteCount(user) > UserLimit)
        {
            throw new BalikobotException(
                BalikobotError.Config,
                "balikobot: invalid configuration: user is required and limited to 100 bytes");
        }

        if (config.ApiKey.Length == 0 || Encoding.UTF8.GetByteCount(config.ApiKey) > ApiKeyLimit)
        {
            throw new BalikobotException(
                BalikobotError.Config,
                "balikobot: invalid configuration: API key is required and limited to 4096 bytes");
        }

        if (config.Timeout < TimeSpan.Zero)
        {
            throw new BalikobotException(
                BalikobotError.Config,
                "balikobot: invalid configuration: timeout must not be negative");
        }

        if (config.MaxResponseBytes < 0 || config.MaxResponseBytes > MaxResponseBytesLimit)
        {
            throw new BalikobotException(
                BalikobotError.Config,
                "balikobot: invalid configuration: response limit must be between 0 and 1073741824 bytes");
        }

        var (baseUrl, origin, loopback) = Wire.ResolveBaseUrl(config.BaseUrl);
        var labelHosts = NormalizeLabelHosts(config.LabelHosts);
        var timeout = config.Timeout == TimeSpan.Zero ? BalikobotConfig.DefaultTimeout : config.Timeout;

        if (config.HttpClient is { } injected)
        {
            Transport = injected;
        }
        else
        {
            Transport = new HttpClient(
                new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.All,
                },
                disposeHandler: true)
            {
                Timeout = timeout,
            };
            _ownsHttpClient = true;
        }

        BaseUrl = baseUrl;
        Origin = origin;
        Loopback = loopback;
        MaxResponseBytes = config.MaxResponseBytes == 0
            ? BalikobotConfig.DefaultMaxResponseBytes
            : config.MaxResponseBytes;
        LabelHosts = labelHosts;
        LiveAccount = config.LiveAccount;
        Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + config.ApiKey));
    }

    internal HttpClient Transport { get; }

    internal string BaseUrl { get; }

    internal string Authorization { get; }

    internal string Origin { get; }

    internal bool Loopback { get; }

    internal int MaxResponseBytes { get; }

    internal IReadOnlyList<string> LabelHosts { get; }

    internal bool? LiveAccount { get; }

    /// <summary>Releases the owned HTTP client. A caller-owned client is left untouched.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsHttpClient)
        {
            Transport.Dispose();
        }
    }

    private static IReadOnlyList<string> NormalizeLabelHosts(IReadOnlyList<string> hosts)
    {
        var normalized = new string[hosts.Count];
        for (var index = 0; index < hosts.Count; index++)
        {
            var host = hosts[index].Trim().ToLowerInvariant();
            if (host.Length == 0 || host.IndexOfAny(HostSeparators) >= 0)
            {
                throw new BalikobotException(
                    BalikobotError.Config,
                    "balikobot: invalid configuration: label hosts must be host names");
            }

            normalized[index] = host;
        }

        return normalized;
    }
}
