namespace Balikobot.Tests;

public class ErrorTests
{
    [Fact]
    public void ExceptionCarriesTheErrorCodeAndRetryHint()
    {
        var retryAfter = TimeSpan.FromSeconds(30);
        var exception = new BalikobotException(
            BalikobotError.Unavailable,
            "balikobot: temporarily unavailable",
            retryAfter);

        Assert.Equal(BalikobotError.Unavailable, exception.Error);
        Assert.Equal(retryAfter, exception.RetryAfter);
        Assert.Equal("balikobot: temporarily unavailable", exception.Message);
    }

    [Fact]
    public void RetryAfterIsNullWithoutAProviderHint()
    {
        var exception = new BalikobotException(BalikobotError.NotFound, "balikobot: resource not found");

        Assert.Equal(BalikobotError.NotFound, exception.Error);
        Assert.Null(exception.RetryAfter);
    }
}
