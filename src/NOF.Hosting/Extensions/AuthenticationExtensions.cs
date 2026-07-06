using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddJwtPropagation()
        {
            builder.Services.AddRequestOutboundMiddleware<JwtTokenPropagationOutboundMiddleware>();
            JwtPropagationRegistrationHooks.Invoke(builder);
            return builder;
        }
    }
}
