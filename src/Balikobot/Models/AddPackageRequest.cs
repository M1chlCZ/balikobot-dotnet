using System.Text.Json.Serialization;
using Balikobot.Codes;

namespace Balikobot;

/// <summary>Describes one package for the ADD method. The external reference EID makes ADD idempotent: a repeated request with an already stored EID returns the original record.</summary>
public sealed record AddPackageRequest
{
    /// <summary>Gets the external package reference. It must contain 8 to 40 alphanumeric or dash characters.</summary>
    [JsonPropertyName("eid")]
    public string Eid { get; init; } = string.Empty;

    /// <summary>Gets the carrier service code, for example "1" or "VMCZ".</summary>
    [JsonPropertyName("service_type")]
    public string ServiceType { get; init; } = string.Empty;

    /// <summary>Gets the recipient name. The name or the firm must be set.</summary>
    [JsonPropertyName("rec_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? RecName { get; init; }

    /// <summary>Gets the recipient company. The name or the firm must be set.</summary>
    [JsonPropertyName("rec_firm")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? RecFirm { get; init; }

    /// <summary>Gets the recipient street. It is required.</summary>
    [JsonPropertyName("rec_street")]
    public string RecStreet { get; init; } = string.Empty;

    /// <summary>Gets the recipient city. It is required.</summary>
    [JsonPropertyName("rec_city")]
    public string RecCity { get; init; } = string.Empty;

    /// <summary>Gets the recipient postal code. It is required.</summary>
    [JsonPropertyName("rec_zip")]
    public string RecZip { get; init; } = string.Empty;

    /// <summary>Gets the ISO 3166-1 alpha-2 destination country. It is required.</summary>
    [JsonPropertyName("rec_country")]
    public CountryCode RecCountry { get; init; }

    /// <summary>Gets the recipient phone. The phone or the email must be set.</summary>
    [JsonPropertyName("rec_phone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? RecPhone { get; init; }

    /// <summary>Gets the recipient email. The phone or the email must be set.</summary>
    [JsonPropertyName("rec_email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? RecEmail { get; init; }

    /// <summary>Gets the pickup branch reference for branch delivery.</summary>
    [JsonPropertyName("branch_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? BranchId { get; init; }

    /// <summary>Gets the package weight in kilograms. It must be positive and at most 10000.</summary>
    [JsonPropertyName("weight")]
    public double WeightKg { get; init; }

    /// <summary>Gets the package length in centimeters. It must be positive and at most 1000.</summary>
    [JsonPropertyName("length")]
    public double LengthCm { get; init; }

    /// <summary>Gets the package width in centimeters. It must be positive and at most 1000.</summary>
    [JsonPropertyName("width")]
    public double WidthCm { get; init; }

    /// <summary>Gets the package height in centimeters. It must be positive and at most 1000.</summary>
    [JsonPropertyName("height")]
    public double HeightCm { get; init; }

    /// <summary>Gets the declared value. It must not be negative and at most 100000000.</summary>
    [JsonPropertyName("price")]
    public double Price { get; init; }

    /// <summary>Gets the cash-on-delivery amount. It must not be negative and at most 100000000. A positive amount requires <see cref="Vs"/>.</summary>
    [JsonPropertyName("cod_price")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double CodPrice { get; init; }

    /// <summary>Gets the cash-on-delivery currency. Every ADD requires CZK or EUR.</summary>
    [JsonPropertyName("cod_currency")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public CurrencyCode CodCurrency { get; init; }

    /// <summary>Gets the cash-on-delivery variable symbol. It must be set exactly when <see cref="CodPrice"/> is positive and must be below 10000000000.</summary>
    [JsonPropertyName("vs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Vs { get; init; }
}
