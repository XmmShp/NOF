using NOF.Annotation;
using NOF.Contract.Extension.Authorization.Jwt;

[assembly: AssemblyInitializeAttribute<NOF.Infrastructure.Extension.Authorization.Jwt.NOFJwtAuthorizationAssemblyInitializer>]

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

internal sealed class NOFJwtAuthorizationAssemblyInitializer : IAssemblyInitializer
{
    private static int _initialized;

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        AutoInjectRegistry.Register(typeof(IJwtAuthorityService), typeof(JwtAuthorityService), Lifetime.Scoped, useFactory: false);
        AutoInjectRegistry.Register(typeof(IJwksService), typeof(JwtAuthorityService), Lifetime.Scoped, useFactory: true);
    }
}
