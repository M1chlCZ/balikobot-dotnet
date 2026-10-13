using System.Text.Json;

namespace Balikobot;

internal static class WebhookWire
{
    internal const int NotificationLimit = 1000;

    internal static bool TryParseNotifications(
        byte[] body,
        out IReadOnlyList<TrackingNotification> notifications)
    {
        notifications = [];
        if (body is null)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (!TryNormalize(root, out var single))
                {
                    return false;
                }

                notifications = [single];
                return true;
            }

            if (root.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var result = new List<TrackingNotification>();
            foreach (var entry in root.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !TryNormalize(entry, out var notification))
                {
                    return false;
                }

                result.Add(notification);
                if (result.Count > NotificationLimit)
                {
                    return false;
                }
            }

            notifications = result;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryNormalize(JsonElement entry, out TrackingNotification notification)
    {
        notification = new TrackingNotification();
        if (!TryReadIdentifier(entry, "package_id", out var packageId) ||
            !TryReadIdentifier(entry, "eid", out var eid) ||
            !TryReadIdentifier(entry, "carrier_id", out var carrierId) ||
            !TrackingWire.TryReadTrackStatusId(entry, "status_id", out var statusId) ||
            !TrackingWire.TryReadTrackStatusId(entry, "status_id_v2", out var statusIdV2) ||
            !TryReadText(entry, "name", out var name) ||
            !TryReadText(entry, "status_text", out var statusText))
        {
            return false;
        }

        if (packageId.Length == 0 && eid.Length == 0 && carrierId.Length == 0)
        {
            return false;
        }

        var status = statusIdV2.Set ? statusIdV2 : statusId;
        if (!status.Set)
        {
            return false;
        }

        var text = name.Length > 0 ? name : statusText;
        if (text.Length == 0 || !ShipmentWire.ValidField(text, TrackingWire.DescriptionLimit))
        {
            return false;
        }

        notification = new TrackingNotification
        {
            PackageId = packageId,
            Eid = eid,
            CarrierId = carrierId,
            StatusId = status.Raw,
            StatusText = text,
        };
        return true;
    }

    private static bool TryReadIdentifier(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        string text;
        if (property.ValueKind == JsonValueKind.String)
        {
            text = property.GetString()!;
            if (text.Length == 0)
            {
                return true;
            }
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

        if (!ShipmentWire.ValidField(text, ShipmentWire.IdentifierLimit))
        {
            return false;
        }

        value = text;
        return true;
    }

    private static bool TryReadText(JsonElement element, string name, out string value)
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
