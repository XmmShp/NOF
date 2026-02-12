using Microsoft.Extensions.DependencyInjection;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Extension methods for registering JWT authorization (OIDC resource server) services.
/// </summary>
public static partial class NOFInfrastructureCoreExtensions
{
    /// <param name="builder">The NOF application builder.</param>
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Adds JWT authorization services that fetch JWKS from a remote authority and configure token validation.
        /// This turns the application into an OIDC resource server that validates tokens issued by the authority.
        /// </summary>
        /// <param name="configureOptions">Action to configure JWT authorization options.</param>
        /// <returns>The NOF application builder for chaining.</returns>
        public INOFAppBuilder AddJwtAuthorization(Action<JwtAuthorizationOptions>? configureOptions = null)
        {
            if (configureOptions is not null)
            {
                builder.Services.Configure(configureOptions);
            }
            else
            {
                builder.Services.AddOptionsInConfiguration<JwtAuthorizationOptions>("NOF:JwtAuthorization");
            }

            builder.AddRegistrationStep(new JwtAuthorizationRegistrationStep());
            builder.AddRegistrationStep(new JwtAuthorizationOutboundMiddlewareStep());

            return builder;
        }

        /// <summary>
        /// Adds JWT authorization services with minimal configuration.
        /// </summary>
        /// <param name="authority">The authority URL (e.g., https://auth.example.com).</param>
        /// <returns>The NOF application builder for chaining.</returns>
        public INOFAppBuilder AddJwtAuthorization(string authority)
        {
            return builder.AddJwtAuthorization(options =>
            {
                options.Authority = authority;
            });
        }
    }
}
