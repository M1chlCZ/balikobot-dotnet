namespace Balikobot;

/// <summary>Reports a Balíkobot failure with a stable <see cref="Error"/> classification.</summary>
public sealed class BalikobotException : Exception
{
    /// <summary>Initializes a new exception.</summary>
    /// <param name="error">The error classification.</param>
    /// <param name="message">The error message.</param>
    /// <param name="retryAfter">The provider retry hint, or <see langword="null"/> when the provider sent none.</param>
    public BalikobotException(BalikobotError error, string message, TimeSpan? retryAfter = null)
        : base(message)
    {
        Error = error;
        RetryAfter = retryAfter;
    }

    /// <summary>Gets the error classification.</summary>
    public BalikobotError Error { get; }

    /// <summary>Gets the provider retry hint, or <see langword="null"/> when the provider sent none.</summary>
    public TimeSpan? RetryAfter { get; }
}
