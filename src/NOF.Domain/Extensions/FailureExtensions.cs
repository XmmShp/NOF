using System.Diagnostics.CodeAnalysis;

namespace NOF;

public static partial class __NOF_Domain_Extensions__
{
    extension(Failure failure)
    {
        [DoesNotReturn]
        public void ThrowAsDomainException()
            => throw new DomainException(failure);
    }
}
