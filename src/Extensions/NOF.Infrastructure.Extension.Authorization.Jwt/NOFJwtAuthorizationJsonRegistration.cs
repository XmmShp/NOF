using NOF.Abstraction;
using NOF.Annotation;
using System.Text.Json;

[assembly: AssemblyInitialize<NOF.Infrastructure.Extension.Authorization.Jwt.NOFJwtAuthorizationJsonRegistration>]

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

internal sealed class NOFJwtAuthorizationJsonRegistration : IAssemblyInitializer
{
    private static int _initialized;

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        JsonSerializerOptions.ConfigureNOFJsonSerializerOptions(options =>
        {
            options.TypeInfoResolverChain.Add(NOFJwtAuthorizationJsonSerializerContext.Default);
        });
    }
}
