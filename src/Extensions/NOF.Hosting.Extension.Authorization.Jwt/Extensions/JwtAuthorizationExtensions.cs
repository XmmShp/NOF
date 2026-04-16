using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;

namespace NOF.Hosting.Extension.Authorization.Jwt;

public static partial class NOFJwtAuthorizationExtensions
{
    extension(INOFAppBuilder builder)
    {
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

            builder.Services.AddRequestOutboundMiddleware<JwtTokenPropagationOutboundMiddleware>();
            return builder;
        }
    }
}
