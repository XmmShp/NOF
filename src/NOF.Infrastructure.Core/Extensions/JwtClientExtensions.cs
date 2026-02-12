using Microsoft.Extensions.DependencyInjection;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Extension methods for registering JWT client (OIDC resource server) services.
/// </summary>
public static partial class NOFInfrastructureCoreExtensions
{
    /// <param name="builder">The NOF application builder.</param>
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Adds JWT client services that fetch JWKS from a remote authority and configure token validation.
        /// This turns the application into an OIDC resource server that validates tokens issued by the authority.
        /// </summary>
        /// <param name="configureOptions">Action to configure JWT client options.</param>
        /// <returns>The NOF application builder for chaining.</returns>
        public INOFAppBuilder AddJwtClient(Action<JwtClientOptions>? configureOptions = null)
        {
            if (configureOptions is not null)
            {
                builder.Services.Configure(configureOptions);
            }
            else
            {
                builder.Services.AddOptionsInConfiguration<JwtClientOptions>("JwtClient");
            }

            // Register the JWKS HTTP client
            builder.Services.AddHttpClient(JwtClientConstants.JwksHttpClientName);

            // Register the JWKS provider as singleton (caches keys, supports refresh)
            builder.Services.AddSingleton<IJwksProvider, HttpJwksProvider>();

            // Register the JWT validation service for transport-agnostic token validation
            builder.Services.AddSingleton<IJwtValidationService, JwtValidationService>();

            return builder;
        }

        /// <summary>
        /// Adds JWT client services with minimal configuration.
        /// </summary>
        /// <param name="authority">The authority URL (e.g., https://auth.example.com).</param>
        /// <returns>The NOF application builder for chaining.</returns>
        public INOFAppBuilder AddJwtClient(string authority)
        {
            return builder.AddJwtClient(options =>
            {
                options.Authority = authority;
            });
        }
    }
}
