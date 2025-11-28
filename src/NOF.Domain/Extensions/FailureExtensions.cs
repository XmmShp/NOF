using System.Diagnostics.CodeAnalysis;

namespace NOF;

public static class FailureExtensions
{
    extension(Failure failure)
    {
        [DoesNotReturn]
        public void ThrowAsDomainException()
            => throw new DomainException(failure);
    }
}
