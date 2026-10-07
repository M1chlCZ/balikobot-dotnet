namespace Balikobot.Codes;

/// <summary>A carrier code used in Balíkobot request paths.</summary>
public readonly record struct CarrierCode
{
    private CarrierCode(string value)
    {
        Value = value;
    }

    /// <summary>Gets the wire value of the code.</summary>
    public string Value { get; }

    /// <summary>The PPL code.</summary>
    public static readonly CarrierCode PPL = new("ppl");
    /// <summary>The DPD code.</summary>
    public static readonly CarrierCode DPD = new("dpd");
    /// <summary>The DPD Czech Republic code.</summary>
    public static readonly CarrierCode DPDCZ = new("dpdcz");
    /// <summary>The DPD Slovakia code.</summary>
    public static readonly CarrierCode DPDSK = new("dpdsk");
    /// <summary>The Geis code.</summary>
    public static readonly CarrierCode GEIS = new("geis");
    /// <summary>The GLS code.</summary>
    public static readonly CarrierCode GLS = new("gls");
    /// <summary>The InTime code.</summary>
    public static readonly CarrierCode INTIME = new("intime");
    /// <summary>The Česká pošta code.</summary>
    public static readonly CarrierCode CP = new("cp");
    /// <summary>The alternative Česká pošta code.</summary>
    public static readonly CarrierCode CESKAPOSTA = new("ceskaposta");
    /// <summary>The Balíkovna code.</summary>
    public static readonly CarrierCode BALIKOVNA = new("balikovna");
    /// <summary>The Zásilkovna code.</summary>
    public static readonly CarrierCode ZASILKOVNA = new("zasilkovna");
    /// <summary>The Slovenská pošta code.</summary>
    public static readonly CarrierCode SP = new("sp");
    /// <summary>The Uloženka code.</summary>
    public static readonly CarrierCode ULOZENKA = new("ulozenka");

    /// <summary>Normalizes a carrier code. The value is trimmed and lowercased, so custom carriers work too.</summary>
    /// <param name="value">The carrier code to parse.</param>
    /// <returns>The normalized carrier code.</returns>
    /// <exception cref="FormatException">The value is not a well-formed carrier code.</exception>
    public static CarrierCode Parse(string value)
    {
        if (TryParse(value, out var code))
        {
            return code;
        }
        throw new FormatException($"'{value}' is not a valid carrier code.");
    }

    /// <summary>Tries to normalize a carrier code.</summary>
    /// <param name="value">The carrier code to parse.</param>
    /// <param name="code">The normalized code when parsing succeeds, otherwise the default code.</param>
    /// <returns><see langword="true"/> when the value is a well-formed carrier code.</returns>
    public static bool TryParse(string? value, out CarrierCode code)
    {
        code = default;
        if (value is null)
        {
            return false;
        }
        var normalized = value.Trim().ToLowerInvariant();
        if (!IsValid(normalized))
        {
            return false;
        }
        code = new CarrierCode(normalized);
        return true;
    }

    /// <summary>Reports whether a value is a well-formed carrier code.</summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> when the value is a well-formed carrier code.</returns>
    public static bool IsValid(string value)
    {
        if (value is null || value.Length is < 2 or > 32)
        {
            return false;
        }
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Returns the wire value of the code.</summary>
    /// <returns>The wire value.</returns>
    public override string ToString() => Value ?? string.Empty;

    internal static CarrierCode FromWire(string value) => new(value);
}
