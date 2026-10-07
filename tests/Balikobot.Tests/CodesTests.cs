using Balikobot.Codes;

namespace Balikobot.Tests;

public class CodesTests
{
    [Fact]
    public void ConstantsExposeTheWireValues()
    {
        Assert.Equal("ppl", CarrierCode.PPL.Value);
        Assert.Equal("ulozenka", CarrierCode.ULOZENKA.Value);
        Assert.Equal("CZK", CurrencyCode.CZK.Value);
        Assert.Equal("DKK", CurrencyCode.DKK.Value);
        Assert.Equal("CZ", CountryCode.CZ.Value);
        Assert.Equal("CA", CountryCode.CA.Value);
        Assert.Equal("ppl", CarrierCode.PPL.ToString());
        Assert.Equal("CZK", CurrencyCode.CZK.ToString());
        Assert.Equal("CZ", CountryCode.CZ.ToString());
    }

    [Fact]
    public void ParseNormalizesCustomValues()
    {
        Assert.Equal("mycarrier99", CarrierCode.Parse(" MyCarrier99 ").Value);
        Assert.Equal("EUR", CurrencyCode.Parse(" eur ").Value);
        Assert.Equal("DE", CountryCode.Parse(" de ").Value);
    }

    [Fact]
    public void ParseRejectsMalformedValues()
    {
        Assert.Throws<FormatException>(() => CarrierCode.Parse("bad code!"));
        Assert.Throws<FormatException>(() => CurrencyCode.Parse("EU"));
        Assert.Throws<FormatException>(() => CountryCode.Parse("D3"));
    }

    [Fact]
    public void TryParseReportsSuccessAndFailure()
    {
        Assert.True(CarrierCode.TryParse(" MyCarrier99 ", out var carrier));
        Assert.Equal("mycarrier99", carrier.Value);
        Assert.False(CarrierCode.TryParse("bad code!", out _));

        Assert.True(CurrencyCode.TryParse(" eur ", out var currency));
        Assert.Equal("EUR", currency.Value);
        Assert.False(CurrencyCode.TryParse("EU", out _));

        Assert.True(CountryCode.TryParse(" de ", out var country));
        Assert.Equal("DE", country.Value);
        Assert.False(CountryCode.TryParse("D3", out _));
    }

    [Fact]
    public void IsValidAcceptsConstantsAndRejectsMalformedValues()
    {
        Assert.True(CarrierCode.IsValid(CarrierCode.GLS.Value));
        Assert.False(CarrierCode.IsValid("GLS"));
        Assert.True(CurrencyCode.IsValid(CurrencyCode.EUR.Value));
        Assert.False(CurrencyCode.IsValid("eur"));
        Assert.True(CountryCode.IsValid(CountryCode.DE.Value));
        Assert.False(CountryCode.IsValid("de"));
    }

    [Fact]
    public void EqualityUsesTheWireValue()
    {
        Assert.Equal(CarrierCode.DPD, CarrierCode.Parse(" DPD "));
        Assert.Equal(CurrencyCode.CZK, CurrencyCode.Parse("czk"));
        Assert.Equal(CountryCode.DE, CountryCode.Parse("de"));
        Assert.NotEqual(CarrierCode.DPD, CarrierCode.PPL);
    }
}
