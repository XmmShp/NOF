using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract.Extension.Authorization.Jwt;
using NOF.Hosting;
using NOF.Hosting.Extension.Authorization.Jwt;
using HttpJwksClient = NOF.Contract.Extension.Authorization.Jwt.HttpJwksService;

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
        /// <param name="configureOptions">Action to configure JWT resource server options.</param>
        /// <returns>The NOF application builder for chaining.</returns>
        public INOFAppBuilder AddJwtResourceServer(Action<JwtResourceServerOptions>? configureOptions = null)
        {
            builder.Services.ReplaceOrAddSingleton<IValidateOptions<JwtResourceServerOptions>, JwtResourceServerOptionsValidator>();

            if (configureOptions is not null)
            {
                builder.Services.Configure(configureOptions);
            }
            else
            {
                builder.Services.AddOptions<JwtResourceServerOptions>();
            }

            builder.Services.ReplaceOrAddSingleton<IConfigureOptions<JwtTokenPropagationOptions>>(
                sp =>
                {
                    var resourceOptions = sp.GetRequiredService<IOptions<JwtResourceServerOptions>>().Value;
                    return new ConfigureOptions<JwtTokenPropagationOptions>(opts =>
                    {
                        opts.HeaderName = resourceOptions.HeaderName;
                        opts.TokenType = resourceOptions.TokenType;
                    });
                });

            builder.AddJwtTokenPropagation();

            builder.Services.AddHttpClient<HttpJwksClient>();
            if (builder.Services.All(sd => sd.ServiceType != typeof(IJwksService)))
            {
                builder.Services.AddScoped<IJwksService>(sp => sp.GetRequiredService<HttpJwksClient>());
            }
            builder.Services.ReplaceOrAddSingleton<IJwksProvider, JwksProvider>();
#pragma warning disable CS8620
            builder.Services.AddHandlerInfo(new NotificationHandlerInfo(typeof(RefreshJwksOnKeyRotation), typeof(JwtKeyRotationNotification)));
#pragma warning restore CS8620

            builder.Services.AddInboundMiddleware<JwtResourceServerInboundMiddleware>();

            return builder;
        }

        /// <summary>
        /// Adds JWT authority services: token issuance, RSA key management with rotation,
        /// local token validation, refresh token lifecycle, and key rotation background service.
        /// </summary>
        /// <param name="configureOptions">Action to configure authority options.</param>
        /// <returns>The NOF application builder for chaining.</returns>
        public JwtAuthoritySelector AddJwtAuthority(Action<JwtAuthorityOptions>? configureOptions = null)
        {
            builder.Services.ReplaceOrAddScoped<IJwtAuthorityService, JwtAuthorityService>();

            if (configureOptions is not null)
            {
                builder.Services.Configure(configureOptions);
            }
            else
            {
                builder.Services.ReplaceOrAddSingleton<IValidateOptions<JwtAuthorityOptions>, JwtAuthorityOptionsValidator>();
                builder.Services.AddOptions<JwtAuthorityOptions>();
            }

            // Signing key service (in-memory key ring)
            builder.Services.ReplaceOrAddSingleton<ISigningKeyService, SigningKeyService>();

            // JWKS service
            builder.Services.ReplaceOrAddSingleton<IJwksService, JwksService>();

            // Key rotation background service
            builder.Services.AddHostedService<JwtKeyRotationBackgroundService>();

            builder.Services.ReplaceOrAddSingleton<IRevokedRefreshTokenRepository, CacheRevokedRefreshTokenRepository>();

            // Bridge JwtAuthorityOptions.Issuer into JwtResourceServerOptions
            builder.Services.ReplaceOrAddSingleton<IConfigureOptions<JwtResourceServerOptions>>(
                sp =>
                {
                    var authorityOptions = sp.GetRequiredService<IOptions<JwtAuthorityOptions>>().Value;
                    return new ConfigureOptions<JwtResourceServerOptions>(opts =>
                    {
                        opts.Issuer ??= authorityOptions.Issuer;
                    });
                });

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

