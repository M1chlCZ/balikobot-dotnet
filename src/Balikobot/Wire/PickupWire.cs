using System.Globalization;
using System.Text.Json;
using Balikobot.Codes;

namespace Balikobot;

internal static class PickupWire
{
    internal const int PackageLimit = 10_000;
    internal const int WeightLimit = 100_000;
    internal const int NoteLimit = 255;

    internal static BalikobotException PickupStatusError(int status)
    {
        return ShipmentWire.Error(status is 400 or 401 or 403 or 404 or 405
            or 413 or 415 or 422 or 429
            ? BalikobotError.Rejected
            : BalikobotError.Ambiguous);
    }

    internal static bool ValidPickupRequest(CarrierCode carrier, PickupRequest request)
    {
        if (request is null ||
            carrier != CarrierCode.DPD && carrier != CarrierCode.DPDCZ && carrier != CarrierCode.PPL)
        {
            return false;
        }

        if (!DateOnly.TryParseExact(
                request.Date,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date) ||
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) != request.Date)
        {
            return false;
        }

        return request.PackageCount is >= 1 and <= PackageLimit &&
            !double.IsNaN(request.WeightKg) &&
            request.WeightKg > 0 &&
            request.WeightKg <= WeightLimit &&
            ShipmentWire.ValidField(request.Note, NoteLimit);
    }

    internal static object RequestBody(CarrierCode carrier, PickupRequest request)
    {
        if (carrier == CarrierCode.PPL)
        {
            return request.Note.Length > 0
                ? new { date = request.Date, note = request.Note }
                : new { date = request.Date };
        }

        return request.Note.Length > 0
            ? new
            {
                date = request.Date,
                weight = request.WeightKg,
                package_count = request.PackageCount,
                message = request.Note,
            }
            : new { date = request.Date, weight = request.WeightKg, package_count = request.PackageCount };
    }

    internal static bool TryDecode(
        byte[] body,
        out int? status,
        out string providerId,
        out bool? confirmed)
    {
        status = null;
        providerId = string.Empty;
        confirmed = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                TryReadStatus(root, out status) &&
                TryReadString(root, "pickup_order_id", out providerId) &&
                TryReadBool(root, "confirmed", out confirmed);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadBool(JsonElement element, string name, out bool? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
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
