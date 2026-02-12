using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers all JWT authorization (OIDC resource server) services:
/// HTTP-based JWKS provider, JWT identity resolver, outbound middleware, and key-rotation handler.
/// <para>
/// If <c>AddJwtAuthority</c> is also used, <see cref="JwtAuthorityRegistrationStep"/>
/// (which declares <see cref="IAfter{T}"/> on this step) will override the JWKS provider
/// with a local implementation.
/// </para>
/// </summary>
public class JwtAuthorizationRegistrationStep : IDependentServiceRegistrationStep
{
    public ValueTask ExecuteAsync(INOFAppBuilder builder)
    {
        // JWKS HTTP client
        builder.Services.AddHttpClient(NOFInfrastructureCoreConstants.JwtClient.JwksHttpClientName);

        // HTTP-based JWKS provider (may be overridden by JwtAuthorityRegistrationStep)
        builder.Services.ReplaceOrAddSingleton<IJwksProvider, HttpJwksProvider>();

        // JWT identity resolver
        builder.Services.AddSingleton<IIdentityResolver, JwtIdentityResolver>();

#pragma warning disable CS8620
        // Key rotation handler
        builder.Services.AddHandlerInfo(
            new HandlerInfo(HandlerKind.Event, typeof(RefreshJwksOnKeyRotation), typeof(JwtKeyRotationNotification), null));
#pragma warning restore CS8620

        return ValueTask.CompletedTask;
    }
}
