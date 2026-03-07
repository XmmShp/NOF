namespace NOF.Domain;

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class ValidationException : DomainException
{
    /// <summary>
    /// Initializes a new instance with the default validation error code.
    /// </summary>
    /// <param name="message">The validation error message.</param>
    public ValidationException(string message)
        : base("400", message)
    {
    }

    /// <summary>
    /// Initializes a new instance with the default validation error code and inner exception.
    /// </summary>
    /// <param name="message">The validation error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ValidationException(string message, Exception innerException)
        : base("400", message, innerException)
    {
    }

    /// <summary>Initializes a new instance from a <see cref="Failure"/>.</summary>
    /// <param name="failure">The failure descriptor.</param>
    public ValidationException(Failure failure) : base(failure)
    {
    }

    /// <summary>Initializes a new instance from a <see cref="Failure"/> with an inner exception.</summary>
    /// <param name="failure">The failure descriptor.</param>
    /// <param name="innerException">The inner exception.</param>
    public ValidationException(Failure failure, Exception innerException) : base(failure, innerException)
    {
    }

    /// <summary>Initializes a new instance with an error code and message.</summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The error message.</param>
    public ValidationException(string errorCode, string message) : base(errorCode, message)
    {
    }

    /// <summary>Initializes a new instance with an error code, message, and inner exception.</summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ValidationException(string errorCode, string message, Exception innerException) : base(errorCode, message, innerException)
    {
    }
}
