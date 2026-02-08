namespace NOF;

/// <summary>
/// Exception thrown when a domain rule is violated.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Gets the application-specific error code.</summary>
    public int ErrorCode { get; }

    /// <summary>Initializes a new instance from a <see cref="Failure"/>.</summary>
    /// <param name="failure">The failure descriptor.</param>
    public DomainException(Failure failure) : this(failure.ErrorCode, failure.Message)
    {
    }

    /// <summary>Initializes a new instance from a <see cref="Failure"/> with an inner exception.</summary>
    /// <param name="failure">The failure descriptor.</param>
    /// <param name="innerException">The inner exception.</param>
    public DomainException(Failure failure, Exception innerException) : this(failure.ErrorCode, failure.Message, innerException)
    {
    }

    /// <summary>Initializes a new instance with an error code and message.</summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The error message.</param>
    public DomainException(int errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Initializes a new instance with an error code, message, and inner exception.</summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DomainException(int errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}