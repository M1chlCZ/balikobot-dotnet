using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Balikobot.Codes;

namespace Balikobot;

internal interface ICapabilityStatusResponse
{
    CapabilityStatus? Status { get; }
}

[JsonConverter(typeof(CapabilityStatusJsonConverter))]
internal readonly record struct CapabilityStatus(int Value);

internal sealed class CapabilityStatusJsonConverter : JsonConverter<CapabilityStatus>
{
    public override CapabilityStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number))
        {
            return new CapabilityStatus(number);
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString() ?? string.Empty;
            if (text.Length is >= 1 and <= 3 &&
                int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return new CapabilityStatus(parsed);
            }
        }

        throw new JsonException("invalid Balíkobot response status");
    }

    public override void Write(Utf8JsonWriter writer, CapabilityStatus value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}

internal sealed class WhoAmIWire : ICapabilityStatusResponse
{
    [JsonPropertyName("status")]
    public CapabilityStatus? Status { get; init; }

    [JsonPropertyName("live_account")]
    public bool? LiveAccount { get; init; }

    [JsonPropertyName("carriers")]
    public List<CapabilityCarrierWire?>? Carriers { get; init; }
}

internal sealed class CapabilityCarrierWire
{
    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal sealed class ActivatedServicesCapabilityResponse : ICapabilityStatusResponse
{
    [JsonPropertyName("status")]
    public CapabilityStatus? Status { get; init; }

    [JsonPropertyName("active_parcel")]
    public bool? ActiveParcel { get; init; }

    [JsonPropertyName("service_types")]
    public List<ActivatedServiceWire?>? ServiceTypes { get; init; }
}

internal sealed class ActivatedServiceWire
{
    [JsonPropertyName("service_type")]
    public JsonElement ServiceType { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("home_delivery")]
    public bool? HomeDelivery { get; init; }

    [JsonPropertyName("box_delivery")]
    public bool? BoxDelivery { get; init; }

    [JsonPropertyName("pickup_points_delivery")]
    public bool? PickupPointsDelivery { get; init; }
}

internal sealed class CountriesCapabilityResponse : ICapabilityStatusResponse
{
    [JsonPropertyName("status")]
    public CapabilityStatus? Status { get; init; }

    [JsonPropertyName("service_types")]
    [JsonConverter(typeof(CountriesServiceTypesJsonConverter))]
    public List<CountriesServiceWire?>? ServiceTypes { get; init; }
}

internal sealed class CountriesServiceTypesJsonConverter : JsonConverter<List<CountriesServiceWire?>>
{
    public override List<CountriesServiceWire?> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            var services = new List<CountriesServiceWire?>(root.GetArrayLength());
            foreach (var item in root.EnumerateArray())
            {
                services.Add(ReadService(item, options));
            }

            return services;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("invalid Balíkobot countries service types");
        }

        var keyed = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Length == 0 || !AllDigits(property.Name))
            {
                throw new JsonException("invalid Balíkobot countries service key");
            }

            keyed[property.Name] = property.Value;
        }

        if (keyed.Count > CapabilityWire.ServiceLimit)
        {
            throw new JsonException("too many Balíkobot countries services");
        }

        var keys = new List<string>(keyed.Keys);
        keys.Sort(StringComparer.Ordinal);
        var ordered = new List<CountriesServiceWire?>(keys.Count);
        foreach (var key in keys)
        {
            ordered.Add(ReadService(keyed[key], options));
        }

        return ordered;
    }

    public override void Write(Utf8JsonWriter writer, List<CountriesServiceWire?> value, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }

    private static CountriesServiceWire? ReadService(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return element.Deserialize<CountriesServiceWire>(options) ??
            throw new JsonException("invalid Balíkobot countries service");
    }

    private static bool AllDigits(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed class CountriesServiceWire
{
    [JsonPropertyName("service_type")]
    public JsonElement ServiceType { get; init; }

    [JsonPropertyName("countries")]
    public List<string?>? Countries { get; init; }
}

internal sealed class CodCapabilityResponse : ICapabilityStatusResponse
{
    [JsonPropertyName("status")]
    public CapabilityStatus? Status { get; init; }

    [JsonPropertyName("service_types")]
    public List<CodServiceWire?>? ServiceTypes { get; init; }
}

internal sealed class CodServiceWire
{
    [JsonPropertyName("service_type")]
    public JsonElement ServiceType { get; init; }

    [JsonPropertyName("countries")]
    public List<CodCountryWire?>? Countries { get; init; }
}

internal sealed class CodCountryWire
{
    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("max_price")]
    public JsonElement MaxPrice { get; init; }
}

internal static class CapabilityWire
{
    internal const int ServiceLimit = 512;
    internal const int CarrierLimit = 128;
    internal const int NameLimit = 512;
    internal const int ServiceCodeByteLimit = 64;

    private const int MajorPriceLimit = 64;
    private const int ExponentLimit = 64;
    private const int MinorUnitsPerCurrency = 100;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    internal static BalikobotException Error(BalikobotError error) => ShipmentWire.Error(error);

    internal static bool TryReadServiceCode(JsonElement element, out string code)
    {
        code = string.Empty;
        string text;
        if (element.ValueKind == JsonValueKind.String)
        {
            text = element.GetString() ?? string.Empty;
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

        if (text.Length == 0 ||
            Encoding.UTF8.GetByteCount(text) > ServiceCodeByteLimit ||
            text.Contains('\r') ||
            text.Contains('\n') ||
            text.Contains('\0'))
        {
            return false;
        }

        code = text;
        return true;
    }

    internal static bool TryReadServiceEntry(JsonElement serviceType, int countryCount, out string code)
    {
        code = string.Empty;
        if (!TryReadServiceCode(serviceType, out var rawCode))
        {
            return false;
        }

        code = rawCode.Trim();
        return ValidCapabilityServiceCode(code) && countryCount <= ServiceLimit;
    }

    internal static bool ValidCapabilityServiceCode(string code)
    {
        if (code.Length == 0 ||
            code.Contains('/') ||
            code.Contains('\\') ||
            code.Contains('\0') ||
            code.Contains('\r') ||
            code.Contains('\n'))
        {
            return false;
        }

        var runes = 0;
        foreach (var rune in code.EnumerateRunes())
        {
            if (Rune.IsControl(rune))
            {
                return false;
            }

            runes++;
            if (runes > ServiceLimit)
            {
                return false;
            }
        }

        return true;
    }

    internal static bool MajorPriceToMinor(JsonElement element, out long minor)
    {
        minor = 0;
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return false;
        }

        var text = element.GetRawText().Trim();
        if (text.Length is 0 or > MajorPriceLimit)
        {
            return false;
        }

        var index = 0;
        var negative = false;
        if (text[index] is '+' or '-')
        {
            negative = text[index] == '-';
            index++;
        }

        var digits = BigInteger.Zero;
        var scale = 0;
        var mantissaDigits = 0;
        var seenPoint = false;
        var exponent = 0L;
        var hasExponent = false;
        for (; index < text.Length; index++)
        {
            var character = text[index];
            if (character is >= '0' and <= '9')
            {
                digits = (digits * 10) + (character - '0');
                mantissaDigits++;
                if (seenPoint)
                {
                    scale--;
                }

                continue;
            }

            if (character == '.' && !seenPoint)
            {
                seenPoint = true;
                continue;
            }

            if (character is 'e' or 'E')
            {
                hasExponent = true;
                index++;
                break;
            }

            return false;
        }

        if (mantissaDigits == 0)
        {
            return false;
        }

        if (hasExponent)
        {
            if (!long.TryParse(
                    text[index..],
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out exponent) ||
                exponent < -ExponentLimit ||
                exponent > ExponentLimit)
            {
                return false;
            }

            scale += (int)exponent;
        }

        var shift = scale + 2;
        if (shift < 0)
        {
            var divisor = BigInteger.Pow(10, -shift);
            if (!BigInteger.Remainder(digits, divisor).IsZero)
            {
                return false;
            }

            digits /= divisor;
        }
        else if (shift > 0)
        {
            digits *= BigInteger.Pow(10, shift);
        }

        if ((negative && !digits.IsZero) || digits > long.MaxValue)
        {
            return false;
        }

        minor = (long)digits;
        return true;
    }

    internal static (List<Service> Services, Dictionary<string, int> Index) NormalizeActivatedServices(
        ActivatedServicesCapabilityResponse activated)
    {
        var entries = activated.ServiceTypes ?? [];
        if (entries.Count > ServiceLimit)
        {
            throw Error(BalikobotError.InvalidResponse);
        }

        var services = new List<Service>(entries.Count);
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var service = NormalizeActivatedService(entry);
            if (activated.ActiveParcel == false)
            {
                continue;
            }

            if (index.TryGetValue(service.Code, out var previous))
            {
                if (!SameService(services[previous], service))
                {
                    throw Error(BalikobotError.InvalidResponse);
                }

                continue;
            }

            index[service.Code] = services.Count;
            services.Add(service);
        }

        return (services, index);
    }

    internal static Service NormalizeActivatedService(ActivatedServiceWire? entry)
    {
        if (entry is null || !TryReadServiceCode(entry.ServiceType, out var rawCode))
        {
            throw Error(BalikobotError.InvalidResponse);
        }

        var code = rawCode.Trim();
        var name = (entry.Name ?? string.Empty).Trim();
        if (!ValidCapabilityServiceCode(code) ||
            name.Length == 0 ||
            RuneCount(name) > NameLimit ||
            ContainsControl(name))
        {
            throw Error(BalikobotError.InvalidResponse);
        }

        return new Service
        {
            Code = code,
            Name = name,
            HomeDelivery = entry.HomeDelivery,
            BoxDelivery = entry.BoxDelivery,
            PickupPointsDelivery = entry.PickupPointsDelivery,
            Countries = new Dictionary<CountryCode, bool>(),
        };
    }

    internal static bool SameService(Service left, Service right)
    {
        return left.Code == right.Code &&
            left.Name == right.Name &&
            left.HomeDelivery == right.HomeDelivery &&
            left.BoxDelivery == right.BoxDelivery &&
            left.PickupPointsDelivery == right.PickupPointsDelivery;
    }

    internal static List<Service> NormalizeCapabilities(
        ActivatedServicesCapabilityResponse activated,
        CountriesCapabilityResponse countries,
        CodCapabilityResponse cod)
    {
        var (services, index) = NormalizeActivatedServices(activated);
        MergeCapabilityCountries(services, index, countries);
        MergeCapabilityCod(services, index, cod);
        return services;
    }

    internal static void MergeCapabilityCountries(
        List<Service> services,
        Dictionary<string, int> index,
        CountriesCapabilityResponse countries)
    {
        var entries = countries.ServiceTypes ?? [];
        if (entries.Count > ServiceLimit)
        {
            throw Error(BalikobotError.InvalidResponse);
        }

        foreach (var entry in entries)
        {
            if (entry is null ||
                !TryReadServiceEntry(entry.ServiceType, entry.Countries?.Count ?? 0, out var code))
            {
                throw Error(BalikobotError.InvalidResponse);
            }

            if (!index.TryGetValue(code, out var serviceIndex))
            {
                continue;
            }

            foreach (var rawCountry in entry.Countries ?? [])
            {
                var normalized = CountryCode.FromWire((rawCountry ?? string.Empty).Trim().ToUpperInvariant());
                if (IsEuCountryCode(normalized))
                {
                    services[serviceIndex].Countries[normalized] = true;
                }
            }
        }
    }

    internal static void MergeCapabilityCod(
        List<Service> services,
        Dictionary<string, int> index,
        CodCapabilityResponse cod)
    {
        var entries = cod.ServiceTypes ?? [];
        if (entries.Count > ServiceLimit)
        {
            throw Error(BalikobotError.InvalidResponse);
        }

        foreach (var entry in entries)
        {
            if (entry is null ||
                !TryReadServiceEntry(entry.ServiceType, entry.Countries?.Count ?? 0, out var code))
            {
                throw Error(BalikobotError.InvalidResponse);
            }

            if (!index.TryGetValue(code, out var serviceIndex))
            {
                continue;
            }

            var merged = MergeCodCountries(services[serviceIndex].Cod, entry.Countries);
            services[serviceIndex] = services[serviceIndex] with { Cod = merged };
        }
    }

    internal static List<CODCapability> MergeCodCountries(
        IReadOnlyList<CODCapability> entries,
        IReadOnlyList<CodCountryWire?>? countries)
    {
        var result = new List<CODCapability>(entries);
        foreach (var wire in countries ?? [])
        {
            var capability = NormalizeCodCapability(wire);
            if (!IsEuCountryCode(capability.Country))
            {
                continue;
            }

            var existing = FindCodCapability(result, capability.Country, capability.Currency);
            if (existing >= 0)
            {
                if (result[existing] != capability)
                {
                    throw Error(BalikobotError.InvalidResponse);
                }

                continue;
            }

            result.Add(capability);
        }

        return result;
    }

    internal static List<CODCapability> NormalizeCodCountries(IReadOnlyList<CodCountryWire?>? countries)
    {
        var result = new List<CODCapability>();
        foreach (var wire in countries ?? [])
        {
            var capability = NormalizeCodCapability(wire);
            var existing = FindCodCapability(result, capability.Country, capability.Currency);
            if (existing >= 0)
            {
                if (result[existing] != capability)
                {
                    throw Error(BalikobotError.InvalidResponse);
                }

                continue;
            }

            result.Add(capability);
        }

        return result;
    }

    internal static CODCapability NormalizeCodCapability(CodCountryWire? wire)
    {
        if (wire is null ||
            !CountryCode.TryParse(wire.Country, out var country) ||
            !CurrencyCode.TryParse(wire.Currency, out var currency) ||
            !MajorPriceToMinor(wire.MaxPrice, out var minor))
        {
            throw Error(BalikobotError.InvalidResponse);
        }

        return new CODCapability
        {
            Country = country,
            Currency = currency,
            MaxAmountMinor = minor,
        };
    }

    internal static bool IsEuCountryCode(CountryCode code)
    {
        return code == CountryCode.AT ||
            code == CountryCode.BE ||
            code == CountryCode.BG ||
            code == CountryCode.HR ||
            code == CountryCode.CY ||
            code == CountryCode.CZ ||
            code == CountryCode.DK ||
            code == CountryCode.EE ||
            code == CountryCode.FI ||
            code == CountryCode.FR ||
            code == CountryCode.DE ||
            code == CountryCode.GR ||
            code == CountryCode.HU ||
            code == CountryCode.IE ||
            code == CountryCode.IT ||
            code == CountryCode.LV ||
            code == CountryCode.LT ||
            code == CountryCode.LU ||
            code == CountryCode.MT ||
            code == CountryCode.NL ||
            code == CountryCode.PL ||
            code == CountryCode.PT ||
            code == CountryCode.RO ||
            code == CountryCode.SK ||
            code == CountryCode.SI ||
            code == CountryCode.ES ||
            code == CountryCode.SE;
    }

    internal static List<Carrier> ScopedCapabilityCarriers(
        IReadOnlyList<CapabilityCarrierWire?>? contracted,
        IReadOnlyList<CarrierCode>? scope)
    {
        var entries = contracted ?? [];
        if (entries.Count > CarrierLimit)
        {
            throw Error(BalikobotError.InvalidResponse);
        }

        var available = new HashSet<CarrierCode>();
        var contractedCodes = new List<CarrierCode>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry is null || !CarrierCode.TryParse(entry.Slug, out var code))
            {
                throw Error(BalikobotError.InvalidResponse);
            }

            available.Add(code);
            contractedCodes.Add(code);
        }

        HashSet<CarrierCode> requested;
        if (scope is null)
        {
            requested = available;
        }
        else
        {
            requested = [];
            foreach (var candidate in scope)
            {
                if (candidate.Value is not { } value ||
                    !CarrierCode.TryParse(value, out var code) ||
                    !available.Contains(code))
                {
                    throw Error(BalikobotError.InvalidResponse);
                }

                requested.Add(code);
            }
        }

        var emitted = new HashSet<CarrierCode>();
        var carriers = new List<Carrier>(requested.Count);
        foreach (var code in contractedCodes)
        {
            if (requested.Contains(code) && emitted.Add(code))
            {
                carriers.Add(new Carrier { CarrierCode = code });
            }
        }

        return carriers;
    }

    private static int FindCodCapability(
        List<CODCapability> entries,
        CountryCode country,
        CurrencyCode currency)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].Country == country && entries[index].Currency == currency)
            {
                return index;
            }
        }

        return -1;
    }

    private static int RuneCount(string value)
    {
        var count = 0;
        foreach (var _ in value.EnumerateRunes())
        {
            count++;
        }

        return count;
    }

    private static bool ContainsControl(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}
