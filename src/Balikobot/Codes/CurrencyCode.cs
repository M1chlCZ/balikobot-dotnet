using System.Text.Json.Serialization;

namespace Balikobot.Codes;

/// <summary>An ISO 4217 currency code used by the Balíkobot API.</summary>
[JsonConverter(typeof(CurrencyCodeJsonConverter))]
public readonly record struct CurrencyCode
{
    private CurrencyCode(string value)
    {
        Value = value;
    }

    /// <summary>Gets the wire value of the code.</summary>
    public string Value { get; }

    /// <summary>The Czech koruna.</summary>
    public static readonly CurrencyCode CZK = new("CZK");
    /// <summary>The euro.</summary>
    public static readonly CurrencyCode EUR = new("EUR");
    /// <summary>The United States dollar.</summary>
    public static readonly CurrencyCode USD = new("USD");
    /// <summary>The pound sterling.</summary>
    public static readonly CurrencyCode GBP = new("GBP");
    /// <summary>The Polish złoty.</summary>
    public static readonly CurrencyCode PLN = new("PLN");
    /// <summary>The Hungarian forint.</summary>
    public static readonly CurrencyCode HUF = new("HUF");
    /// <summary>The Romanian leu.</summary>
    public static readonly CurrencyCode RON = new("RON");
    /// <summary>The Bulgarian lev.</summary>
    public static readonly CurrencyCode BGN = new("BGN");
    /// <summary>The Croatian kuna.</summary>
    public static readonly CurrencyCode HRK = new("HRK");
    /// <summary>The Swiss franc.</summary>
    public static readonly CurrencyCode CHF = new("CHF");
    /// <summary>The Norwegian krone.</summary>
    public static readonly CurrencyCode NOK = new("NOK");
    /// <summary>The Swedish krona.</summary>
    public static readonly CurrencyCode SEK = new("SEK");
    /// <summary>The Danish krone.</summary>
    public static readonly CurrencyCode DKK = new("DKK");

    /// <summary>Normalizes a currency code. The value is trimmed and uppercased, so custom currencies work too.</summary>
    /// <param name="value">The currency code to parse.</param>
    /// <returns>The normalized currency code.</returns>
    /// <exception cref="FormatException">The value is not a well-formed currency code.</exception>
    public static CurrencyCode Parse(string value)
    {
        if (TryParse(value, out var code))
        {
            return code;
        }
        throw new FormatException($"'{value}' is not a valid currency code.");
    }

    /// <summary>Tries to normalize a currency code.</summary>
    /// <param name="value">The currency code to parse.</param>
    /// <param name="code">The normalized code when parsing succeeds, otherwise the default code.</param>
    /// <returns><see langword="true"/> when the value is a well-formed currency code.</returns>
    public static bool TryParse(string? value, out CurrencyCode code)
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
        code = new CurrencyCode(normalized);
        return true;
    }

    /// <summary>Reports whether a value is a well-formed currency code.</summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> when the value is a well-formed currency code.</returns>
    public static bool IsValid(string value)
    {
        if (value is null || value.Length != 3)
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

    internal static CurrencyCode FromWire(string value) => new(value);
}
