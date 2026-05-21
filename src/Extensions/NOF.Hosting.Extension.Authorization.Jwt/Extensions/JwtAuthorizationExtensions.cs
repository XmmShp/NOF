using Microsoft.Extensions.DependencyInjection;

namespace NOF.Hosting.Extension.Authorization.Jwt;

public static partial class NOFJwtAuthorizationExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddJwtTokenPropagation()
        {
            builder.Services.AddRequestOutboundMiddleware<JwtTokenPropagationOutboundMiddleware>();
            return builder;
        }
    }
}
