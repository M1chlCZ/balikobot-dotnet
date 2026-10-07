using System.Globalization;
using System.Text;
using System.Text.Json;
using Balikobot.Codes;

namespace Balikobot;

internal static class ShipmentWire
{
    internal const int IdentifierLimit = 100;
    internal const int AddFieldLimit = 255;
    internal const int LabelResponseLimit = 4 * 1024 * 1024;

    internal readonly record struct AddEntry(
        string Eid,
        int? Status,
        string? PackageId,
        string CarrierId,
        string LabelUrl);

    internal readonly record struct OverviewEntry(
        string Eid,
        string? PackageId,
        string CarrierId,
        string LabelUrl);

    internal static bool ValidEid(string value)
    {
        if (value is null || value.Length is < 8 or > 40)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-')
            {
                return false;
            }
        }

        return true;
    }

    internal static bool ValidService(string value)
    {
        if (value is null || value.Length is < 1 or > 16)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool ValidBranchId(string value)
    {
        if (value is null || value.Length is < 1 or > 64 || !char.IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool ValidField(string? value, int maximum)
    {
        if (value is null)
        {
            return false;
        }

        var runes = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is '\r' or '\n' or '\0')
            {
                return false;
            }

            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    return false;
                }

                index++;
            }
            else if (char.IsLowSurrogate(character))
            {
                return false;
            }

            runes++;
            if (runes > maximum)
            {
                return false;
            }
        }

        return true;
    }

    internal static bool ValidPackageId(string? value)
    {
        return value is { Length: > 0 } && ValidField(value, IdentifierLimit);
    }

    internal static bool ValidAddRequest(AddPackageRequest request)
    {
        if (request is null ||
            !ValidEid(request.Eid) ||
            !ValidService(request.ServiceType) ||
            request.CodCurrency != CurrencyCode.CZK && request.CodCurrency != CurrencyCode.EUR)
        {
            return false;
        }

        if (request.RecName is not { Length: > 0 } && request.RecFirm is not { Length: > 0 })
        {
            return false;
        }

        if (request.RecPhone is not { Length: > 0 } && request.RecEmail is not { Length: > 0 })
        {
            return false;
        }

        if (request.CodPrice > 0 && request.Vs is null)
        {
            return false;
        }

        if (request.CodPrice == 0 && request.Vs is not null)
        {
            return false;
        }

        foreach (var field in new[]
                 {
                     request.RecName,
                     request.RecFirm,
                     request.RecStreet,
                     request.RecCity,
                     request.RecZip,
                     request.RecPhone,
                     request.RecEmail,
                 })
        {
            if (!ValidField(field ?? string.Empty, AddFieldLimit))
            {
                return false;
            }
        }

        if (request.RecStreet is not { Length: > 0 } ||
            request.RecCity is not { Length: > 0 } ||
            request.RecZip is not { Length: > 0 } ||
            !CountryCode.IsValid(request.RecCountry.Value))
        {
            return false;
        }

        if (request.BranchId is { Length: > 0 } branchId && !ValidBranchId(branchId))
        {
            return false;
        }

        if (request.WeightKg <= 0 || request.WeightKg > 10_000 ||
            request.LengthCm <= 0 || request.LengthCm > 1_000 ||
            request.WidthCm <= 0 || request.WidthCm > 1_000 ||
            request.HeightCm <= 0 || request.HeightCm > 1_000)
        {
            return false;
        }

        if (request.Price < 0 || request.Price > 100_000_000 ||
            request.CodPrice < 0 || request.CodPrice > 100_000_000)
        {
            return false;
        }

        return request.Vs is not { } vs || vs is >= 0 and < 10_000_000_000;
    }

    internal static BalikobotError? TopLevelStatusError(int status)
    {
        return status switch
        {
            200 or 208 => null,
            426 or 503 => BalikobotError.Unavailable,
            400 or 402 or 403 or 404 or 405 or 406 or 409 or 413 or 423 or 501 => BalikobotError.Rejected,
            _ => BalikobotError.InvalidResponse,
        };
    }

    internal static BalikobotException? LabelLookupStatus(RawResponse response)
    {
        if (response.Status == 429)
        {
            return Unavailable(Wire.RetryAfter(response));
        }

        if (response.Status >= 500)
        {
            return Error(BalikobotError.Unavailable);
        }

        if (response.Status != 200 || !Wire.IsJson(response))
        {
            return Error(response.Status is >= 400 and < 500
                ? BalikobotError.Rejected
                : BalikobotError.InvalidResponse);
        }

        return null;
    }

    internal static BalikobotException Error(BalikobotError error)
    {
        return new BalikobotException(error, Message(error));
    }

    internal static BalikobotException Unavailable(TimeSpan? retryAfter)
    {
        return new BalikobotException(BalikobotError.Unavailable, "balikobot: temporarily unavailable", retryAfter);
    }

    internal static bool TryDecodeAdd(byte[] body, out int? status, out List<AddEntry> packages)
    {
        status = null;
        packages = [];
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryReadStatus(root, out status))
            {
                return false;
            }

            if (!root.TryGetProperty("packages", out var list) || list.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (list.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var element in list.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Null)
                {
                    packages.Add(new AddEntry(string.Empty, null, null, string.Empty, string.Empty));
                    continue;
                }

                if (element.ValueKind != JsonValueKind.Object ||
                    !TryReadString(element, "eid", out var eid) ||
                    !TryReadStatus(element, out var entryStatus) ||
                    !TryReadPackageId(element, "package_id", out var packageId) ||
                    !TryReadString(element, "carrier_id", out var carrierId) ||
                    !TryReadString(element, "label_url", out var labelUrl))
                {
                    return false;
                }

                packages.Add(new AddEntry(eid, entryStatus, packageId, carrierId, labelUrl));
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool TryDecodeOverview(byte[] body, out int? status, out List<OverviewEntry> packages)
    {
        status = null;
        packages = [];
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryReadStatus(root, out status))
            {
                return false;
            }

            if (!root.TryGetProperty("packages", out var list) || list.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (list.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var element in list.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Null)
                {
                    packages.Add(new OverviewEntry(string.Empty, null, string.Empty, string.Empty));
                    continue;
                }

                if (element.ValueKind != JsonValueKind.Object ||
                    !TryReadString(element, "eid", out var eid) ||
                    !TryReadPackageId(element, "package_id", out var packageId) ||
                    !TryReadString(element, "carrier_id", out var carrierId) ||
                    !TryReadString(element, "label_url", out var labelUrl))
                {
                    return false;
                }

                packages.Add(new OverviewEntry(eid, packageId, carrierId, labelUrl));
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool TryDecodeLabels(byte[] body, out int? status, out string labelUrl)
    {
        status = null;
        labelUrl = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                TryReadStatus(root, out status) &&
                TryReadString(root, "labels_url", out labelUrl);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool TryDecodeOrderView(
        byte[] body,
        out int? status,
        out string orderId,
        out List<string> packageIds,
        out string labelUrl)
    {
        status = null;
        orderId = string.Empty;
        packageIds = [];
        labelUrl = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryReadStatus(root, out status) ||
                !TryReadString(root, "order_id", out orderId))
            {
                return false;
            }

            if (root.TryGetProperty("package_ids", out var list) && list.ValueKind != JsonValueKind.Null)
            {
                if (list.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                foreach (var element in list.EnumerateArray())
                {
                    if (!TryReadPackageIdValue(element, out var packageId))
                    {
                        return false;
                    }

                    packageIds.Add(packageId);
                }
            }

            return TryReadString(root, "labels_url", out labelUrl);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static async Task<DownloadedLabel> DownloadLabelAsync(
        BalikobotClient client,
        string labelUrl,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, labelUrl);
        HttpResponseMessage response;
        try
        {
            response = await client.Transport
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Error(BalikobotError.Unavailable);
        }
        catch (HttpRequestException)
        {
            throw Error(BalikobotError.Unavailable);
        }
        catch (IOException)
        {
            throw Error(BalikobotError.Unavailable);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (status == 429 || status >= 500)
            {
                throw Error(BalikobotError.Unavailable);
            }

            if (status != 200)
            {
                throw Error(status is >= 400 and < 500 ? BalikobotError.Rejected : BalikobotError.InvalidResponse);
            }

            if (!Wire.TryParseMediaType(response.Content.Headers.ContentType?.ToString(), out var mediaType))
            {
                throw Error(BalikobotError.InvalidResponse);
            }

            byte[] body;
            try
            {
                body = await ReadLimitedBodyAsync(response.Content, LabelResponseLimit, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (RequestFailureException exception)
            {
                throw Error(exception.Kind == RequestFailureKind.BodyLimit
                    ? BalikobotError.InvalidResponse
                    : BalikobotError.Unavailable);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw Error(BalikobotError.Unavailable);
            }

            var pdf = string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase);
            var zpl = string.Equals(mediaType, "application/zpl", StringComparison.OrdinalIgnoreCase);
            if (!pdf && !zpl)
            {
                throw Error(BalikobotError.InvalidResponse);
            }

            if (body.Length == 0)
            {
                throw Error(BalikobotError.InvalidResponse);
            }

            var prefix = pdf ? "%PDF-"u8 : "^X"u8;
            if (!body.AsSpan().StartsWith(prefix))
            {
                throw Error(BalikobotError.InvalidResponse);
            }

            return new DownloadedLabel(body, pdf ? "application/pdf" : "application/zpl");
        }
    }

    private static bool TryReadStatus(JsonElement element, out int? status)
    {
        status = null;
        if (!element.TryGetProperty("status", out var value))
        {
            return true;
        }

        status = ReadStatus(value);
        return status is not null;
    }

    private static int? ReadStatus(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            var raw = element.GetRawText();
            if (raw.Contains('.') || raw.Contains('e') || raw.Contains('E'))
            {
                return null;
            }

            return element.TryGetInt32(out var number) ? number : null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = element.GetString()!;
        if (text.Length is < 1 or > 3)
        {
            return null;
        }

        foreach (var character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return null;
            }
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static bool TryReadString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()!;
        return true;
    }

    private static bool TryReadPackageId(JsonElement element, string name, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property))
        {
            return true;
        }

        if (!TryReadPackageIdValue(property, out var text))
        {
            return false;
        }

        value = text;
        return true;
    }

    private static bool TryReadPackageIdValue(JsonElement property, out string value)
    {
        value = string.Empty;
        string text;
        if (property.ValueKind == JsonValueKind.String)
        {
            text = property.GetString()!;
        }
        else if (property.ValueKind == JsonValueKind.Number)
        {
            text = property.GetRawText();
            if (text.Contains('.') || text.Contains('e') || text.Contains('E'))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        if (!ValidWirePackageId(text))
        {
            return false;
        }

        value = text;
        return true;
    }

    private static bool ValidWirePackageId(string value)
    {
        if (value.Length == 0 || value.Length > IdentifierLimit)
        {
            return false;
        }

        var bytes = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is '\r' or '\n' or '\0')
            {
                return false;
            }

            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    return false;
                }

                bytes += 4;
                index++;
            }
            else if (char.IsLowSurrogate(character))
            {
                return false;
            }
            else
            {
                bytes += Encoding.UTF8.GetByteCount(value.AsSpan(index, 1));
            }

            if (bytes > IdentifierLimit)
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<byte[]> ReadLimitedBodyAsync(
        HttpContent content,
        int limit,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var buffer = new byte[Math.Min(limit + 1, 32 * 1024)];
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RequestFailureException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new RequestFailureException(RequestFailureKind.Transport, exception);
        }
    }

    private static string Message(BalikobotError error)
    {
        return error switch
        {
            BalikobotError.InvalidRequest => "balikobot: invalid request",
            BalikobotError.Rejected => "balikobot: provider permanently rejected the request",
            BalikobotError.Unavailable => "balikobot: temporarily unavailable",
            BalikobotError.Ambiguous => "balikobot: request outcome is unknown",
            _ => "balikobot: invalid provider response",
        };
    }
}
