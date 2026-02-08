using System.Diagnostics.CodeAnalysis;

namespace NOF;

/// <summary>
/// Extension methods for the NOF.Domain layer.
/// </summary>
public static partial class __NOF_Domain_Extensions__
{
    extension(Failure failure)
    {
        /// <summary>Throws a <see cref="DomainException"/> from this failure.</summary>
        [DoesNotReturn]
        public void ThrowAsDomainException()
            => throw new DomainException(failure);
    }
}
