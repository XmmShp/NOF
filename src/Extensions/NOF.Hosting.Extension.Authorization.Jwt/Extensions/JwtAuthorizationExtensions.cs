using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;

namespace NOF.Hosting.Extension.Authorization.Jwt;

/// <summary>
/// Extension methods for enabling JWT-based outbound authorization propagation.
/// </summary>
public static partial class NOFJwtAuthorizationExtensions
{
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Adds JWT authorization support for outbound pipeline propagation.
        /// </summary>
        public INOFAppBuilder AddJwtTokenPropagation(Action<JwtTokenPropagationOptions>? configureOptions = null)
        {
            if (configureOptions is not null)
            {
                builder.Services.Configure(configureOptions);
            }
            else
            {
                builder.Services.AddOptions<JwtTokenPropagationOptions>();
            }

            builder.AddRegistrationStep(new JwtTokenPropagationOutboundMiddlewareStep());

            return builder;
        }
    }
}
