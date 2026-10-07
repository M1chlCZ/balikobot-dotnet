using System.Globalization;
using System.Text.Json;

namespace Balikobot;

internal static class TrackingWire
{
    internal const int DescriptionLimit = 200;

    internal readonly record struct TrackStatusId(string Raw, bool Set);

    internal readonly record struct TrackStatusPackage(
        string CarrierId,
        TrackStatusId StatusId,
        TrackStatusId StatusIdV2,
        string Name,
        string StatusText,
        int? Status);

    internal static BalikobotException? TrackHttpStatus(RawResponse response)
    {
        if (response.Status == 429)
        {
            return ShipmentWire.Unavailable(Wire.RetryAfter(response));
        }

        if (response.Status >= 500)
        {
            return ShipmentWire.Error(BalikobotError.Unavailable);
        }

        if (response.Status == 404)
        {
            return ShipmentWire.Error(BalikobotError.NotFound);
        }

        if (response.Status != 200 || !Wire.IsJson(response))
        {
            return ShipmentWire.Error(response.Status is >= 400 and < 500
                ? BalikobotError.Unavailable
                : BalikobotError.InvalidResponse);
        }

        return null;
    }

    internal static bool TryDecodeTrackStatus(
        byte[] body,
        out int? status,
        out List<TrackStatusPackage> packages)
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
                if (element.ValueKind != JsonValueKind.Object ||
                    !TryReadString(element, "carrier_id", out var carrierId) ||
                    !TryReadTrackStatusId(element, "status_id", out var statusId) ||
                    !TryReadTrackStatusId(element, "status_id_v2", out var statusIdV2) ||
                    !TryReadString(element, "name", out var name) ||
                    !TryReadString(element, "status_text", out var statusText) ||
                    !TryReadStatus(element, out var entryStatus))
                {
                    return false;
                }

                packages.Add(new TrackStatusPackage(carrierId, statusId, statusIdV2, name, statusText, entryStatus));
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool TryDecodeOrder(byte[] body, out int? status, out string orderId)
    {
        status = null;
        orderId = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                TryReadStatus(root, out status) &&
                TryReadString(root, "order_id", out orderId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool TryDecodeDrop(byte[] body, out int? status)
    {
        status = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && TryReadStatus(root, out status);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadTrackStatusId(JsonElement element, string name, out TrackStatusId id)
    {
        id = default;
        if (!element.TryGetProperty(name, out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        var raw = property.GetRawText();
        if (!IsTrackStatusId(raw))
        {
            return false;
        }

        id = new TrackStatusId(raw, true);
        return true;
    }

    private static bool IsTrackStatusId(string text)
    {
        var index = 0;
        if (index < text.Length && text[index] == '-')
        {
            index++;
        }

        var digits = 0;
        while (index < text.Length && digits < 3 && char.IsAsciiDigit(text[index]))
        {
            index++;
            digits++;
        }

        if (digits == 0)
        {
            return false;
        }

        if (index == text.Length)
        {
            return true;
        }

        if (text[index] != '.')
        {
            return false;
        }

        index++;
        var fraction = 0;
        while (index < text.Length && fraction < 2 && char.IsAsciiDigit(text[index]))
        {
            index++;
            fraction++;
        }

        return fraction > 0 && index == text.Length;
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
}
