using Microsoft.Extensions.DependencyInjection;
using NOF;

namespace NOF;

/// <summary>
/// Extension methods for registering JWT authentication services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds JWT authentication services to the service collection.
        /// </summary>
        /// <param name="configureOptions">Action to configure JWT options.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddJwtAuthentication(Action<JwtOptions>? configureOptions = null)
        {
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }

            return services;
        }

        /// <summary>
        /// Adds JWT authentication services with minimal configuration.
        /// </summary>
        /// <param name="issuer">The token issuer.</param>
        /// <param name="audience">The token audience.</param>
        /// <param name="securityKey">The security key for signing tokens.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddJwtAuthentication(string issuer, string audience, string securityKey)
        {
            return services.AddJwtAuthentication(options =>
            {
                options.Issuer = issuer;
                options.Audience = audience;
                options.SecurityKey = securityKey;
            });
        }
    }
}
