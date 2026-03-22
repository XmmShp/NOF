using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NOF.Contract;
using System.Text.Json;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Extension methods for registering JWT authorization (OIDC resource server) and authority services.
/// </summary>
public static partial class NOFJwtAuthorizationExtensions
{
    /// <param name="builder">The NOF application builder.</param>
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Adds JWT authorization services that fetch JWKS from a remote authority and configure token validation.
        /// This turns the application into an OIDC resource server that validates tokens issued by the authority.
        /// </summary>
        /// <param name="configureOptions">Action to configure JWT authorization options.</param>
        /// <returns>A <see cref="JwtAuthorizationSelector"/> for further configuration.</returns>
        public JwtAuthorizationSelector AddJwtAuthorization(Action<JwtAuthorizationOptions>? configureOptions = null)
        {
            if (configureOptions is not null)
            {
                builder.Services.Configure(configureOptions);
            }
            else
            {
                builder.Services.AddOptions<JwtAuthorizationOptions>();
            }

            // Register JwksJsonContext into the shared NOF JSON resolver chain
            JsonSerializerOptions.ConfigureNOFJsonSerializerOptions(options =>
            {
                options.TypeInfoResolverChain.Add(JwksJsonContext.Default);
            });

            // JWKS HTTP client
            builder.Services.AddHttpClient(NOFJwtAuthorizationConstants.JwtClient.JwksHttpClientName);

            // HTTP-based JWKS provider (TryAdd 鈥?authority's local provider wins if registered)
            builder.Services.TryAddSingleton<IJwksProvider, HttpJwksProvider>();

            // Inbound/outbound JWT middleware steps
            builder.AddRegistrationStep(new JwtAuthorizationInboundMiddlewareStep());

            builder.AddRegistrationStep(new JwtAuthorizationOutboundMiddlewareStep());

#pragma warning disable CS8620
            // Key rotation handler
            builder.Services.AddHandlerInfo(
                new NotificationHandlerInfo(typeof(RefreshJwksOnKeyRotation), typeof(JwtKeyRotationNotification)));
#pragma warning restore CS8620

            return new JwtAuthorizationSelector(builder);
        }

        /// <summary>
        /// Adds JWT authorization services with minimal configuration.
        /// </summary>
        /// <param name="jwksEndpoint">The JWKS endpoint URL (e.g., https://auth.example.com/.well-known/jwks.json).</param>
        /// <returns>A <see cref="JwtAuthorizationSelector"/> for further configuration.</returns>
        public JwtAuthorizationSelector AddJwtAuthorization(string jwksEndpoint)
        {
            return builder.AddJwtAuthorization(options =>
            {
                options.JwksEndpoint = jwksEndpoint;
            });
        }

        /// <summary>
        /// Adds JWT authority services: token issuance, RSA key management with rotation,
        /// local token validation, refresh token lifecycle, and key rotation background service.
        /// </summary>
        /// <param name="configureOptions">Action to configure authority options.</param>
        /// <returns>The NOF application builder for chaining.</returns>
        public JwtAuthoritySelector AddJwtAuthority(Action<JwtAuthorityOptions>? configureOptions = null)
        {
            if (configureOptions is not null)
            {
                builder.Services.Configure(configureOptions);
            }
            else
            {
                builder.Services.AddSingleton<IValidateOptions<JwtAuthorityOptions>, JwtAuthorityOptionsValidator>();
                builder.Services.AddOptions<JwtAuthorityOptions>();
            }

            // Signing key service (in-memory key ring)
            builder.Services.AddSingleton<ISigningKeyService, SigningKeyService>();

            // JWKS service
            builder.Services.AddSingleton<IJwksService, JwksService>();

            // Key rotation background service
            builder.Services.AddHostedService<JwtKeyRotationBackgroundService>();

            // Local JWKS provider 鈥?overrides HttpJwksProvider (authority always uses local)
            builder.Services.ReplaceOrAddSingleton<IJwksProvider, LocalJwksProvider>();

            // Revoked refresh token repository (TryAdd 鈥?user can override with custom impl)
            builder.Services.AddSingleton<IRevokedRefreshTokenRepository, CacheRevokedRefreshTokenRepository>();

            // Bridge JwtAuthorityOptions.Issuer into JwtAuthorizationOptions
            builder.Services.AddSingleton<IConfigureOptions<JwtAuthorizationOptions>>(
                sp =>
                {
                    var authorityOptions = sp.GetRequiredService<IOptions<JwtAuthorityOptions>>().Value;
                    return new ConfigureOptions<JwtAuthorizationOptions>(opts =>
                    {
                        opts.Issuer ??= authorityOptions.Issuer;
                    });
                });

#pragma warning disable CS8620
            // Register handlers 鈥?AddHandlerInfo handles keyed services + endpoint name map at runtime
            builder.Services.AddHandlerInfo(
                new RequestWithResponseHandlerInfo(typeof(GenerateJwtToken), typeof(GenerateJwtTokenRequest), typeof(GenerateJwtTokenResponse)),
                new RequestWithResponseHandlerInfo(typeof(ValidateJwtRefreshToken), typeof(ValidateJwtRefreshTokenRequest), typeof(ValidateJwtRefreshTokenResponse)));
            builder.Services.AddHandlerInfo(
                new RequestWithoutResponseHandlerInfo(typeof(RevokeJwtRefreshToken), typeof(RevokeJwtRefreshTokenRequest)));
#pragma warning restore CS8620

            return new JwtAuthoritySelector(builder);
        }

        /// <summary>
        /// Adds JWT authority services with minimal configuration.
        /// </summary>
        /// <param name="issuer">The token issuer.</param>
        /// <returns>A <see cref="JwtAuthoritySelector"/> for further configuration.</returns>
        public JwtAuthoritySelector AddJwtAuthority(string issuer)
        {
            return builder.AddJwtAuthority(options =>
            {
                options.Issuer = issuer;
            });
        }
    }

}
