using System.Text.Json.Serialization;

namespace Balikobot.Codes;

/// <summary>An ISO 3166-1 alpha-2 country code used by the Balíkobot API.</summary>
[JsonConverter(typeof(CountryCodeJsonConverter))]
public readonly record struct CountryCode
{
    private CountryCode(string value)
    {
        Value = value;
    }

    /// <summary>Gets the wire value of the code.</summary>
    public string Value { get; }

    /// <summary>Austria.</summary>
    public static readonly CountryCode AT = new("AT");
    /// <summary>Belgium.</summary>
    public static readonly CountryCode BE = new("BE");
    /// <summary>Bulgaria.</summary>
    public static readonly CountryCode BG = new("BG");
    /// <summary>Croatia.</summary>
    public static readonly CountryCode HR = new("HR");
    /// <summary>Cyprus.</summary>
    public static readonly CountryCode CY = new("CY");
    /// <summary>Czechia.</summary>
    public static readonly CountryCode CZ = new("CZ");
    /// <summary>Denmark.</summary>
    public static readonly CountryCode DK = new("DK");
    /// <summary>Estonia.</summary>
    public static readonly CountryCode EE = new("EE");
    /// <summary>Finland.</summary>
    public static readonly CountryCode FI = new("FI");
    /// <summary>France.</summary>
    public static readonly CountryCode FR = new("FR");
    /// <summary>Germany.</summary>
    public static readonly CountryCode DE = new("DE");
    /// <summary>Greece.</summary>
    public static readonly CountryCode GR = new("GR");
    /// <summary>Hungary.</summary>
    public static readonly CountryCode HU = new("HU");
    /// <summary>Ireland.</summary>
    public static readonly CountryCode IE = new("IE");
    /// <summary>Italy.</summary>
    public static readonly CountryCode IT = new("IT");
    /// <summary>Latvia.</summary>
    public static readonly CountryCode LV = new("LV");
    /// <summary>Lithuania.</summary>
    public static readonly CountryCode LT = new("LT");
    /// <summary>Luxembourg.</summary>
    public static readonly CountryCode LU = new("LU");
    /// <summary>Malta.</summary>
    public static readonly CountryCode MT = new("MT");
    /// <summary>The Netherlands.</summary>
    public static readonly CountryCode NL = new("NL");
    /// <summary>Poland.</summary>
    public static readonly CountryCode PL = new("PL");
    /// <summary>Portugal.</summary>
    public static readonly CountryCode PT = new("PT");
    /// <summary>Romania.</summary>
    public static readonly CountryCode RO = new("RO");
    /// <summary>Slovakia.</summary>
    public static readonly CountryCode SK = new("SK");
    /// <summary>Slovenia.</summary>
    public static readonly CountryCode SI = new("SI");
    /// <summary>Spain.</summary>
    public static readonly CountryCode ES = new("ES");
    /// <summary>Sweden.</summary>
    public static readonly CountryCode SE = new("SE");
    /// <summary>The United Kingdom.</summary>
    public static readonly CountryCode GB = new("GB");
    /// <summary>Switzerland.</summary>
    public static readonly CountryCode CH = new("CH");
    /// <summary>Norway.</summary>
    public static readonly CountryCode NO = new("NO");
    /// <summary>Iceland.</summary>
    public static readonly CountryCode IS = new("IS");
    /// <summary>Liechtenstein.</summary>
    public static readonly CountryCode LI = new("LI");
    /// <summary>Ukraine.</summary>
    public static readonly CountryCode UA = new("UA");
    /// <summary>Serbia.</summary>
    public static readonly CountryCode RS = new("RS");
    /// <summary>Bosnia and Herzegovina.</summary>
    public static readonly CountryCode BA = new("BA");
    /// <summary>Montenegro.</summary>
    public static readonly CountryCode ME = new("ME");
    /// <summary>North Macedonia.</summary>
    public static readonly CountryCode MK = new("MK");
    /// <summary>Albania.</summary>
    public static readonly CountryCode AL = new("AL");
    /// <summary>Türkiye.</summary>
    public static readonly CountryCode TR = new("TR");
    /// <summary>The United States.</summary>
    public static readonly CountryCode US = new("US");
    /// <summary>Canada.</summary>
    public static readonly CountryCode CA = new("CA");

    /// <summary>Normalizes a country code. The value is trimmed and uppercased, so custom countries work too.</summary>
    /// <param name="value">The country code to parse.</param>
    /// <returns>The normalized country code.</returns>
    /// <exception cref="FormatException">The value is not a well-formed country code.</exception>
    public static CountryCode Parse(string value)
    {
        if (TryParse(value, out var code))
        {
            return code;
        }
        throw new FormatException($"'{value}' is not a valid country code.");
    }

    /// <summary>Tries to normalize a country code.</summary>
    /// <param name="value">The country code to parse.</param>
    /// <param name="code">The normalized code when parsing succeeds, otherwise the default code.</param>
    /// <returns><see langword="true"/> when the value is a well-formed country code.</returns>
    public static bool TryParse(string? value, out CountryCode code)
    {
        code = default;
        if (value is null)
        {
            return false;
        }
        var normalized = value.Trim().ToUpperInvariant();
        if (!IsValid(normalized))
        {
            return false;
        }
        code = new CountryCode(normalized);
        return true;
    }

    /// <summary>Reports whether a value is a well-formed country code.</summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> when the value is a well-formed country code.</returns>
    public static bool IsValid(string value)
    {
        if (value is null || value.Length != 2)
        {
            return false;
        }
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterUpper(character))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Returns the wire value of the code.</summary>
    /// <returns>The wire value.</returns>
    public override string ToString() => Value ?? string.Empty;

    internal static CountryCode FromWire(string value) => new(value);
}
