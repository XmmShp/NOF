namespace NOF.Contract;

/// <summary>
/// Marker interface for result types that represent the outcome of an operation,
/// either success or failure.
/// </summary>
public interface IResult
{
    /// <summary>Indicates whether the operation succeeded.</summary>
    bool IsSuccess { get; }

    /// <summary>Error code when failed; empty when succeeded.</summary>
    string ErrorCode { get; }

    /// <summary>Human-readable message; empty when succeeded.</summary>
    string Message { get; }

    /// <summary>The success payload when available; otherwise <see langword="null"/>.</summary>
    object? Value { get; }

    /// <summary>
    /// Additional metadata to accompany the result.
    /// </summary>
    IDictionary<string, string> Extra { get; }
}
public interface IResult<TSelf> : IResult
    where TSelf : IResult<TSelf>
{
    static abstract TSelf From(IResult other);
}
