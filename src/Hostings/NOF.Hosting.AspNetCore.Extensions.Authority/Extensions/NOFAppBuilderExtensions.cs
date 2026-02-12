using Microsoft.Extensions.DependencyInjection;
using NOF.Infrastructure.Core;

namespace NOF.Hosting.AspNetCore.Extensions.Authority;

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
        public INOFAppBuilder AddJwtAuthority(Action<AuthorityOptions>? configureOptions = null)
        {
            if (configureOptions is not null)
            {
                builder.Services.Configure(configureOptions);
            }
            else
            {
                builder.Services.AddOptionsInConfiguration<AuthorityOptions>("NOF:Authority");
            }

            builder.AddRegistrationStep(new JwtAuthorityRegistrationStep());

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
