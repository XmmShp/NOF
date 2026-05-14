using NOF.Abstraction;
using System.Text.Json;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

internal static class NOFJwtAuthorizationJsonRegistration
{
    private static int _initialized;

    public static void EnsureRegistered()
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
