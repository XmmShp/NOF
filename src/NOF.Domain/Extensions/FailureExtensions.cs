using System.Diagnostics.CodeAnalysis;

namespace NOF;

/// <summary>
/// Extension methods for the NOF.Domain layer.
/// </summary>
public static partial class NOFDomainExtensions
{
    extension(Failure failure)
    {
        /// <summary>Throws a <see cref="DomainException"/> from this failure.</summary>
        [DoesNotReturn]
        public void ThrowAsDomainException()
            => throw new DomainException(failure);
    }
}
