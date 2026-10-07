using System.Text;

namespace Balikobot.Tests;

public class ConfigTests
{
    [Fact]
    public void DefaultsMatchTheGoReference()
    {
        var config = new BalikobotConfig();

        Assert.Equal("https://apiv2.balikobot.cz", BalikobotConfig.DefaultBaseUrl);
        Assert.Equal(TimeSpan.FromSeconds(30), BalikobotConfig.DefaultTimeout);
        Assert.Equal(8 * 1024 * 1024, BalikobotConfig.DefaultMaxResponseBytes);
        Assert.Equal(BalikobotConfig.DefaultBaseUrl, config.BaseUrl);
        Assert.Equal(BalikobotConfig.DefaultTimeout, config.Timeout);
        Assert.Equal(BalikobotConfig.DefaultMaxResponseBytes, config.MaxResponseBytes);
        Assert.Empty(config.LabelHosts);
        Assert.Null(config.LiveAccount);
        Assert.Null(config.HttpClient);
        Assert.Equal(string.Empty, config.User);
        Assert.Equal(string.Empty, config.ApiKey);
    }

    [Fact]
    public void ZeroTimeoutAndLimitSelectTheDefaults()
    {
        using var client = new BalikobotClient(new BalikobotConfig
        {
            User = "api-user",
            ApiKey = "key",
            Timeout = TimeSpan.Zero,
            MaxResponseBytes = 0,
        });

        Assert.Equal(BalikobotConfig.DefaultTimeout, client.Transport.Timeout);
        Assert.Equal(BalikobotConfig.DefaultMaxResponseBytes, client.MaxResponseBytes);
    }

    [Fact]
    public void BaseUrlTrailingSlashAndUserWhitespaceAreNormalized()
    {
        using var client = new BalikobotClient(new BalikobotConfig
        {
            BaseUrl = "https://apiv2.balikobot.cz/",
            User = "  api-user  ",
            ApiKey = "provider-secret",
        });

        Assert.Equal("https://apiv2.balikobot.cz", client.BaseUrl);
        Assert.Equal(
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("api-user:provider-secret")),
            client.Authorization);
    }

    [Fact]
    public void NullCredentialsThrowConfig()
    {
        var userException = Assert.Throws<BalikobotException>(() => new BalikobotClient(new BalikobotConfig
        {
            User = null!,
            ApiKey = "key",
        }));

        var keyException = Assert.Throws<BalikobotException>(() => new BalikobotClient(new BalikobotConfig
        {
            User = "api-user",
            ApiKey = null!,
        }));

        Assert.Equal(BalikobotError.Config, userException.Error);
        Assert.Equal(BalikobotError.Config, keyException.Error);
    }

    [Fact]
    public void InvalidConfigurationThrowsConfig()
    {
        var invalid = new[]
        {
            new BalikobotConfig { User = "", ApiKey = "key" },
            new BalikobotConfig { User = "api-user", ApiKey = "" },
            new BalikobotConfig { User = new string('a', 101), ApiKey = "key" },
            new BalikobotConfig { User = "api-user", ApiKey = new string('a', 4097) },
            new BalikobotConfig { User = "api-user", ApiKey = "key", Timeout = TimeSpan.FromSeconds(-1) },
            new BalikobotConfig { User = "api-user", ApiKey = "key", MaxResponseBytes = -1 },
            new BalikobotConfig { User = "api-user", ApiKey = "key", MaxResponseBytes = (1 << 30) + 1 },
            new BalikobotConfig { User = "api-user", ApiKey = "key", BaseUrl = "not-a-url" },
            new BalikobotConfig { User = "api-user", ApiKey = "key", BaseUrl = "ftp://127.0.0.1:9000" },
            new BalikobotConfig { User = "api-user", ApiKey = "key", BaseUrl = "http://127.0.0.1:9000/api" },
            new BalikobotConfig { User = "api-user", ApiKey = "key", BaseUrl = "http://127.0.0.1:9000?a=1" },
            new BalikobotConfig { User = "api-user", ApiKey = "key", BaseUrl = "http://127.0.0.1:9000#frag" },
            new BalikobotConfig { User = "api-user", ApiKey = "key", BaseUrl = "http://user@127.0.0.1:9000" },
            new BalikobotConfig { User = "api-user", ApiKey = "key", LabelHosts = new[] { "bad/host" } },
            new BalikobotConfig { User = "api-user", ApiKey = "key", LabelHosts = new[] { "" } },
        };

        foreach (var config in invalid)
        {
            var exception = Assert.Throws<BalikobotException>(() => new BalikobotClient(config));
            Assert.Equal(BalikobotError.Config, exception.Error);
        }
    }
}
