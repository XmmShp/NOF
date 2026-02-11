using System.Diagnostics.CodeAnalysis;

namespace NOF.Domain;

/// <summary>
/// Represents a domain failure with a message and error code.
/// </summary>
/// <param name="Message">The failure message.</param>
/// <param name="ErrorCode">The application-specific error code.</param>
public record Failure(string Message, int ErrorCode)
{
    /// <summary>Throws a <see cref="DomainException"/> from this failure.</summary>
    [DoesNotReturn]
    public void ThrowAsDomainException()
        => throw new DomainException(this);
}
