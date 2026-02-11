using Microsoft.Extensions.DependencyInjection;

namespace NOF;

/// <summary>
/// Extension methods for registering JWT authority services in an ASP.NET Core hosted NOF application.
/// </summary>
public static partial class NOFHostingAspNetCoreExtensionsAuthorityExtensions
{
    /// <param name="builder">The NOF application builder.</param>
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Adds JWT authority services and exposes the standard JWKS endpoint at /.well-known/jwks.json.
        /// Also configures JWT Bearer authentication so the authority can validate its own tokens
        /// without making HTTP calls to itself.
        /// </summary>
        /// <param name="configureOptions">Action to configure JWT options.</param>
        /// <returns>The NOF application builder for chaining.</returns>
        public INOFAppBuilder AddJwtAuthority(Action<JwtOptions>? configureOptions = null)
        {
            // Register the core JWT extension assembly for handler discovery
            builder.Assemblies.Add(typeof(SigningKeyService).Assembly);

            // Configure and validate JWT options
            if (configureOptions is not null)
            {
                builder.Services.Configure(configureOptions);
            }
            else
            {
                builder.Services.AddOptionsInConfiguration<JwtOptions>("Jwt");
            }

            // Register the signing key service (singleton — holds the in-memory key ring)
            builder.Services.AddSingleton<ISigningKeyService, SigningKeyService>();

            // Register the JWKS service
            builder.Services.AddSingleton<IJwksService, JwksService>();

            // Register the local JWKS provider so the authority can validate tokens without HTTP round-trip
            builder.Services.AddSingleton<IJwksProvider, LocalJwksProvider>();

            // Map the /.well-known/jwks.json endpoint
            builder.AddInitializationStep(new JwksEndpointInitializationStep());

            return builder;
        }

        /// <summary>
        /// Adds JWT authority services with minimal configuration.
        /// </summary>
        /// <param name="issuer">The token issuer.</param>
        /// <returns>The NOF application builder for chaining.</returns>
        public INOFAppBuilder AddJwtAuthority(string issuer)
        {
            return builder.AddJwtAuthority(options =>
            {
                options.Issuer = issuer;
            });
        }
    }
}
