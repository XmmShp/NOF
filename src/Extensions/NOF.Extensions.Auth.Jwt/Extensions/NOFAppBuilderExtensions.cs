using Microsoft.Extensions.DependencyInjection;

namespace NOF;

/// <summary>
/// Extension methods for registering JWT authentication services.
/// </summary>
public static partial class __NOF_Extensions_Auth_Jwt_Extensions__
{
    /// <param name="builder">The NOF application builder.</param>
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Adds JWT authentication services to the NOF application builder.
        /// </summary>
        /// <param name="configureOptions">Action to configure JWT options.</param>
        /// <returns>The NOF application builder for chaining.</returns>
        public INOFAppBuilder AddJwtAuthentication(Action<JwtOptions>? configureOptions = null)
        {
            // Register this assembly for handler discovery
            builder.Assemblies.Add(typeof(KeyDerivationService).Assembly);

            // Configure and validate JWT options
            if (configureOptions is not null)
            {
                builder.Services.Configure(configureOptions);
            }
            else
            {
                // Bind from configuration by default
                builder.Services.AddOptionsInConfiguration<JwtOptions>("Jwt");
            }

            // Register the key derivation service
            builder.Services.AddSingleton<IKeyDerivationService, KeyDerivationService>();

            return builder;
        }

        /// <summary>
        /// Adds JWT authentication services with minimal configuration to the NOF application builder.
        /// </summary>
        /// <param name="issuer">The token issuer.</param>
        /// <param name="masterSecurityKey">The master security key for deriving client-specific keys.</param>
        /// <returns>The NOF application builder for chaining.</returns>
        public INOFAppBuilder AddJwtAuthentication(string issuer, string masterSecurityKey)
        {
            return builder.AddJwtAuthentication(options =>
            {
                options.Issuer = issuer;
                options.MasterSecurityKey = masterSecurityKey;
            });
        }
    }
}
