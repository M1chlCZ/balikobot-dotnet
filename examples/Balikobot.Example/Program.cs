using Balikobot;
using Balikobot.Codes;

using var client = new BalikobotClient(new BalikobotConfig
{
    User = "api-user",
    ApiKey = "api-key",
    Timeout = TimeSpan.FromSeconds(15),
});

var result = await client.AddPackageAsync(CarrierCode.PPL, new AddPackageRequest
{
    Eid = "order-2026-000123-S1",
    ServiceType = "1",
    RecName = "Example Recipient",
    RecStreet = "Example 1",
    RecCity = "Praha",
    RecZip = "11000",
    RecCountry = CountryCode.CZ,
    RecPhone = "+420777000000",
    WeightKg = 1.5,
    LengthCm = 30,
    WidthCm = 20,
    HeightCm = 10,
    Price = 1000,
    CodCurrency = CurrencyCode.CZK,
});

var labelUrl = await client.LabelsAsync(CarrierCode.PPL, result.PackageId);
Console.WriteLine(labelUrl);

var status = await client.TrackStatusAsync(CarrierCode.PPL, result.CarrierId);
Console.WriteLine(status.StatusText);
