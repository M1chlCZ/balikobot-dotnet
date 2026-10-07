using System.Text.Json;
using System.Text.Json.Serialization;

namespace Balikobot.Codes;

internal sealed class CarrierCodeJsonConverter : JsonConverter<CarrierCode>
{
    public override CarrierCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("balikobot: carrier code must be a string");
        }

        return CarrierCode.FromWire(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, CarrierCode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value ?? string.Empty);
    }
}

internal sealed class CurrencyCodeJsonConverter : JsonConverter<CurrencyCode>
{
    public override CurrencyCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("balikobot: currency code must be a string");
        }

        return CurrencyCode.FromWire(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, CurrencyCode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value ?? string.Empty);
    }
}

internal sealed class CountryCodeJsonConverter : JsonConverter<CountryCode>
{
    public override CountryCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("balikobot: country code must be a string");
        }

        return CountryCode.FromWire(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, CountryCode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value ?? string.Empty);
    }
}
