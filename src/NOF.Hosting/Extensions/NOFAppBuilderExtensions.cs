using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddNOFHosting()
        {
            builder.ConfigureNOFHostEnvironment();
            builder.Services.AddNOFHosting();
            return builder;
        }
    }
}
