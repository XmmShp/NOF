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
            JwtPropagationRegistrationState.MarkJwtPropagationAdded(builder);
            return builder;
        }
    }
}
