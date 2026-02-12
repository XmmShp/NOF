using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Infrastructure.Core;

namespace NOF.Hosting.AspNetCore.Extensions.Authority;

/// <summary>
/// Registers all JWT authority services: signing key service, JWKS service,
/// key rotation background service, local JWKS provider, options bridge, and handlers.
/// <para>
/// Declares <see cref="IAfter{T}"/> on <see cref="JwtAuthorizationRegistrationStep"/> so
/// <see cref="LocalJwksProvider"/> always overrides <see cref="HttpJwksProvider"/>
/// regardless of the order the user calls <c>AddJwtAuthority</c> and <c>AddJwtAuthorization</c>.
/// </para>
/// </summary>
public class JwtAuthorityRegistrationStep : IDependentServiceRegistrationStep, IAfter<JwtAuthorizationRegistrationStep>
{
    public ValueTask ExecuteAsync(INOFAppBuilder builder)
    {
        // Signing key service (in-memory key ring)
        builder.Services.AddSingleton<ISigningKeyService, SigningKeyService>();

        // JWKS service
        builder.Services.AddSingleton<IJwksService, JwksService>();

        // Key rotation background service
        builder.Services.AddHostedService<JwtKeyRotationBackgroundService>();

        // Local JWKS provider — overrides HttpJwksProvider from JwtAuthorizationRegistrationStep
        builder.Services.ReplaceOrAddSingleton<IJwksProvider, LocalJwksProvider>();

        // Bridge AuthorityOptions.Issuer into JwtAuthorizationOptions
        builder.Services.AddSingleton<Microsoft.Extensions.Options.IConfigureOptions<JwtAuthorizationOptions>>(
            sp =>
            {
                var authorityOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorityOptions>>().Value;
                return new Microsoft.Extensions.Options.ConfigureOptions<JwtAuthorizationOptions>(opts =>
                {
                    opts.Issuer ??= authorityOptions.Issuer;
                });
            });

#pragma warning disable CS8620
        // Register handlers
        builder.Services.AddHandlerInfo(
            new HandlerInfo(HandlerKind.RequestWithResponse, typeof(GenerateJwtToken), typeof(GenerateJwtTokenRequest), typeof(GenerateJwtTokenResponse)),
            new HandlerInfo(HandlerKind.RequestWithResponse, typeof(JwtValidateRefreshToken), typeof(JwtValidateRefreshTokenRequest), typeof(JwtValidateRefreshTokenResponse)),
            new HandlerInfo(HandlerKind.RequestWithoutResponse, typeof(JwtRevokeRefreshToken), typeof(JwtRevokeRefreshTokenRequest), null));
#pragma warning restore CS8620

        return ValueTask.CompletedTask;
    }
}
