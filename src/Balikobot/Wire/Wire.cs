using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Balikobot;

internal enum RequestFailureKind
{
    Transport,
    Timeout,
    ConnectionRefusedOrDns,
    BodyLimit,
    AccountUnverified,
}

internal sealed class RequestFailureException : Exception
{
    internal RequestFailureException(RequestFailureKind kind, Exception? inner = null)
        : base($"balikobot: request failed ({kind})", inner)
    {
        Kind = kind;
    }

    internal RequestFailureKind Kind { get; }
}

internal sealed record RawResponse(int Status, HttpResponseHeaders Headers, byte[] Body, string? ContentType);

internal static class Wire
{
    private const int ReadBufferSize = 32 * 1024;
    private const int MaxRetryAfterSeconds = 3600;

    internal static async Task<RawResponse> RequestAsync(
        BalikobotClient client,
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        if (method != HttpMethod.Get)
        {
            await client.VerifyWriteAllowedAsync(cancellationToken).ConfigureAwait(false);
        }

        using var request = new HttpRequestMessage(method, client.BaseUrl + "/" + path.TrimStart('/'));
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("Authorization", client.Authorization);
        if (body is not null)
        {
            request.Content = new ByteArrayContent(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, body.GetType())));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        try
        {
            using var response = await client.Transport
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            var bytes = await ReadLimitedBodyAsync(response.Content, client.MaxResponseBytes, cancellationToken)
                .ConfigureAwait(false);
            var contentType = response.Content.Headers.ContentType?.ToString();

            return new RawResponse((int)response.StatusCode, response.Headers, bytes, contentType);
        }
        catch (RequestFailureException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RequestFailureException(RequestFailureKind.Timeout, exception);
        }
        catch (HttpRequestException exception) when (IsConnectionRefusedOrDns(exception))
        {
            throw new RequestFailureException(RequestFailureKind.ConnectionRefusedOrDns, exception);
        }
        catch (HttpRequestException exception)
        {
            throw new RequestFailureException(RequestFailureKind.Transport, exception);
        }
        catch (IOException exception)
        {
            throw new RequestFailureException(RequestFailureKind.Transport, exception);
        }
    }

    internal static bool IsJson(RawResponse response)
    {
        if (response.ContentType is not { } contentType)
        {
            return false;
        }

        var parts = contentType.Split(';');
        if (!parts[0].Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (var index = 1; index < parts.Length; index++)
        {
            var equals = parts[index].IndexOf('=');
            if (equals <= 0 || parts[index][..equals].Trim().Length == 0)
            {
                return false;
            }
        }

        return true;
    }

    internal static TimeSpan? RetryAfter(RawResponse response)
    {
        if (!response.Headers.TryGetValues("Retry-After", out var values))
        {
            return null;
        }

        var raw = values.FirstOrDefault();
        if (raw is null ||
            !int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < 1)
        {
            return null;
        }

        return TimeSpan.FromSeconds(Math.Min(seconds, MaxRetryAfterSeconds));
    }

    internal static bool ValidLabelUrl(BalikobotClient client, string raw)
    {
        var schemeEnd = raw.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return false;
        }

        var authorityStart = schemeEnd + 3;
        var authorityEnd = raw.IndexOfAny(['/', '?', '#'], authorityStart);
        if (authorityEnd < 0 || raw[authorityEnd] != '/')
        {
            return false;
        }

        var authority = raw[authorityStart..authorityEnd];
        if (authority.Length == 0 || authority.Contains('@'))
        {
            return false;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || uri.Fragment.Length != 0)
        {
            return false;
        }

        var query = uri.Query.Length == 0 ? string.Empty : uri.Query[1..];
        if (query.Length != 0 && query != "zpl=1")
        {
            return false;
        }

        if (client.LabelHosts.Count > 0)
        {
            return LabelHostAllowed(uri, client.LabelHosts);
        }

        if (client.Loopback)
        {
            return uri.Scheme + "://" + uri.Authority == client.Origin;
        }

        var host = uri.Authority.ToLowerInvariant();
        return uri.Scheme == Uri.UriSchemeHttps &&
            (host == "pdf.balikobot.cz" || host.EndsWith(".balikobot.cz", StringComparison.Ordinal));
    }

    internal static (string BaseUrl, string Origin, bool Loopback) ResolveBaseUrl(string? raw)
    {
        var baseUrl = (raw ?? string.Empty).Trim();
        if (baseUrl.Length == 0)
        {
            baseUrl = BalikobotConfig.DefaultBaseUrl;
        }

        if (baseUrl.EndsWith('/'))
        {
            baseUrl = baseUrl[..^1];
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Host.Length == 0)
        {
            throw InvalidBaseUrl("balikobot: invalid configuration: base URL must be absolute without path, query or fragment");
        }

        var schemeEnd = baseUrl.IndexOf("://", StringComparison.Ordinal);
        var authorityEnd = schemeEnd < 0 ? -1 : baseUrl.IndexOfAny(['/', '?', '#'], schemeEnd + 3);
        var authority = schemeEnd < 0
            ? string.Empty
            : baseUrl[(schemeEnd + 3)..(authorityEnd < 0 ? baseUrl.Length : authorityEnd)];
        var suffix = authorityEnd < 0 ? string.Empty : baseUrl[authorityEnd..];
        if (authority.Length == 0 || authority.Contains('@') || suffix is not ("" or "/"))
        {
            throw InvalidBaseUrl("balikobot: invalid configuration: base URL must be absolute without path, query or fragment");
        }

        var address = IPAddress.TryParse(uri.Host, out var parsedAddress) ? parsedAddress : null;
        var loopback = address is not null && IPAddress.IsLoopback(address);
        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return (baseUrl, uri.Scheme + "://" + uri.Authority, loopback);
        }

        if (uri.Scheme == Uri.UriSchemeHttp && loopback)
        {
            return (baseUrl, uri.Scheme + "://" + uri.Authority, loopback);
        }

        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            throw InvalidBaseUrl("balikobot: invalid configuration: base URL must use https unless the host is loopback");
        }

        throw InvalidBaseUrl("balikobot: invalid configuration: base URL must use http or https");
    }

    private static async Task<byte[]> ReadLimitedBodyAsync(
        HttpContent content,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[Math.Min(limit + 1, ReadBufferSize)];
        using var body = new MemoryStream();
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return body.ToArray();
            }

            if (body.Length + read > limit)
            {
                throw new RequestFailureException(RequestFailureKind.BodyLimit);
            }

            body.Write(buffer, 0, read);
        }
    }

    private static bool IsConnectionRefusedOrDns(HttpRequestException exception)
    {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is SocketException socket &&
                socket.SocketErrorCode is SocketError.ConnectionRefused or SocketError.HostNotFound
                    or SocketError.TryAgain or SocketError.NoData)
            {
                return true;
            }
        }

        return false;
    }

    private static bool LabelHostAllowed(Uri uri, IReadOnlyList<string> allowedHosts)
    {
        if (!LabelSchemeAllowed(uri))
        {
            return false;
        }

        var host = uri.Authority.ToLowerInvariant();
        var hostname = uri.Host.ToLowerInvariant();
        foreach (var allowed in allowedHosts)
        {
            if (allowed[0] == '.')
            {
                if (hostname.EndsWith(allowed, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (host == allowed)
            {
                return true;
            }
        }

        return false;
    }

    private static bool LabelSchemeAllowed(Uri uri)
    {
        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }

        return uri.Scheme == Uri.UriSchemeHttp &&
            IPAddress.TryParse(uri.Host, out var address) &&
            IPAddress.IsLoopback(address);
    }

    private static BalikobotException InvalidBaseUrl(string message) => new(BalikobotError.Config, message);
}
