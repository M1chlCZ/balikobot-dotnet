namespace Balikobot;

/// <summary>Classifies a <see cref="BalikobotException"/> so callers can react to it.</summary>
public enum BalikobotError
{
    /// <summary>The arguments were rejected locally before any network call.</summary>
    InvalidRequest,
    /// <summary>The provider permanently rejected the request or the supplied data.</summary>
    Rejected,
    /// <summary>The provider is temporarily unavailable or the request never left the client.</summary>
    Unavailable,
    /// <summary>The provider has no data yet for the requested resource.</summary>
    NotFound,
    /// <summary>A mutating call may have reached the provider; reconcile before any retry.</summary>
    Ambiguous,
    /// <summary>The provider answer violates the protocol.</summary>
    InvalidResponse,
    /// <summary>The client configuration is invalid.</summary>
    Config,
    /// <summary>A typed code value is malformed.</summary>
    InvalidCode,
}
