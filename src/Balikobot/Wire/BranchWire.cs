using System.Globalization;
using System.Text.Json;
using Balikobot.Codes;

namespace Balikobot;

internal static class BranchWire
{
    private const int BranchFieldLimit = 200;
    private const int ZipLimit = 16;

    internal static (string Path, bool FilterCountry) BranchesPath(
        CarrierCode carrier,
        string service,
        CountryCode country)
    {
        var path = "/" + carrier.Value + "/branches/service/" + service;
        if (carrier == CarrierCode.PPL ||
            carrier == CarrierCode.DPD ||
            carrier == CarrierCode.DPDCZ ||
            carrier == CarrierCode.DPDSK ||
            carrier == CarrierCode.GEIS ||
            carrier == CarrierCode.GLS ||
            carrier == CarrierCode.INTIME)
        {
            return (path + "/country/" + country.Value, false);
        }

        if (carrier == CarrierCode.CP ||
            carrier == CarrierCode.CESKAPOSTA ||
            carrier == CarrierCode.BALIKOVNA)
        {
            return (path + "/country/" + country.Value, true);
        }

        if (carrier == CarrierCode.ZASILKOVNA)
        {
            return ("/" + carrier.Value + "/branches/country/" + country.Value, false);
        }

        return (path, true);
    }

    internal static bool TryDecode(byte[] body, out int status, out List<Branch> branches)
    {
        status = 0;
        branches = [];
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var fields = Fields(root);
            return TryReadStatus(fields, out status) && TryReadBranches(fields, branches);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Dictionary<string, JsonElement> Fields(JsonElement element)
    {
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            fields[property.Name] = property.Value;
        }

        return fields;
    }

    private static bool TryReadStatus(Dictionary<string, JsonElement> fields, out int status)
    {
        status = 0;
        if (!fields.TryGetValue("status", out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetInt32(out status);
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString()!;
        if (text.Length is < 1 or > 3)
        {
            return false;
        }

        foreach (var character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out status);
    }

    private static bool TryReadBranches(Dictionary<string, JsonElement> fields, List<Branch> branches)
    {
        if (!fields.TryGetValue("branches", out var list) || list.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (list.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in list.EnumerateArray())
            {
                if (!TryReadBranch(element, out var branch))
                {
                    return false;
                }

                if (branch is not null)
                {
                    branches.Add(branch);
                }
            }

            return true;
        }

        if (list.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var keyed = new Dictionary<string, JsonElement>();
        foreach (var property in list.EnumerateObject())
        {
            keyed[property.Name] = property.Value;
        }

        var keys = new List<string>(keyed.Keys);
        keys.Sort(CompareKeys);
        foreach (var key in keys)
        {
            if (!TryReadBranch(keyed[key], out var branch))
            {
                return false;
            }

            if (branch is not null)
            {
                branches.Add(branch);
            }
        }

        return true;
    }

    private static int CompareKeys(string left, string right)
    {
        var leftNumeric = TryNumericKey(left, out var leftValue);
        var rightNumeric = TryNumericKey(right, out var rightValue);
        if (leftNumeric && rightNumeric)
        {
            return leftValue.CompareTo(rightValue);
        }

        if (leftNumeric)
        {
            return -1;
        }

        if (rightNumeric)
        {
            return 1;
        }

        return string.CompareOrdinal(left, right);
    }

    private static bool TryNumericKey(string key, out long value)
    {
        return long.TryParse(key, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadBranch(JsonElement element, out Branch? branch)
    {
        branch = null;
        if (element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var fields = Fields(element);
        if (!TryReadString(fields, "type", out var type) ||
            !TryReadId(fields, "branch_id", out var branchId) ||
            !TryReadId(fields, "id", out var id) ||
            !TryReadString(fields, "name", out var name) ||
            !TryReadString(fields, "street", out var street) ||
            !TryReadString(fields, "city", out var city) ||
            !TryReadString(fields, "zip", out var zip) ||
            !TryReadString(fields, "country", out var country))
        {
            return false;
        }

        var identifier = branchId ?? id;
        if (identifier is null ||
            !ValidBranchField(name, BranchFieldLimit) ||
            !ValidBranchField(street, BranchFieldLimit) ||
            !ValidBranchField(city, BranchFieldLimit) ||
            !ValidBranchField(zip, ZipLimit))
        {
            return true;
        }

        if (name.Length == 0)
        {
            name = zip;
        }

        if (name.Length == 0 || (country.Length > 0 && !CountryCode.IsValid(country)))
        {
            return true;
        }

        var coordinates = BranchCoordinates(
            ReadCoordinate(fields, "lat"),
            ReadCoordinate(fields, "lng"),
            ReadCoordinate(fields, "latitude"),
            ReadCoordinate(fields, "longitude"));

        branch = new Branch
        {
            Id = identifier,
            Type = type,
            Name = name,
            Street = street,
            City = city,
            Zip = zip,
            Country = CountryCode.FromWire(country),
            Latitude = coordinates?.Latitude,
            Longitude = coordinates?.Longitude,
        };

        return true;
    }

    private static bool TryReadString(
        Dictionary<string, JsonElement> fields,
        string name,
        out string value)
    {
        value = string.Empty;
        if (!fields.TryGetValue(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString()!;
        return true;
    }

    private static bool TryReadId(
        Dictionary<string, JsonElement> fields,
        string name,
        out string? value)
    {
        value = null;
        if (!fields.TryGetValue(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        string text;
        if (element.ValueKind == JsonValueKind.String)
        {
            text = element.GetString()!;
        }
        else if (element.ValueKind == JsonValueKind.Number)
        {
            text = element.GetRawText();
            if (text.Contains('.') || text.Contains('e') || text.Contains('E'))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        if (!IsBranchId(text))
        {
            return true;
        }

        value = text;
        return true;
    }

    private static bool IsBranchId(string value)
    {
        if (value.Length is < 1 or > 64 || !char.IsAsciiLetterOrDigit(value[0]))
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

    private static double? ReadCoordinate(Dictionary<string, JsonElement> fields, string name)
    {
        if (!fields.TryGetValue(name, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            return double.TryParse(
                element.GetRawText(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number)
                ? number
                : null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = element.GetString()!;
        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static (double Latitude, double Longitude)? BranchCoordinates(
        double? lat,
        double? lng,
        double? latitude,
        double? longitude)
    {
        if (lat is { } latValue && lng is { } lngValue && ValidCoordinates(latValue, lngValue))
        {
            return (latValue, lngValue);
        }

        if (latitude is { } latitudeValue &&
            longitude is { } longitudeValue &&
            ValidCoordinates(latitudeValue, longitudeValue))
        {
            return (latitudeValue, longitudeValue);
        }

        return null;
    }

    private static bool ValidCoordinates(double latitude, double longitude)
    {
        return latitude is >= -90 and <= 90 &&
            longitude is >= -180 and <= 180 &&
            (latitude != 0 || longitude != 0);
    }

    private static bool ValidBranchField(string value, int maximum)
    {
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
}
