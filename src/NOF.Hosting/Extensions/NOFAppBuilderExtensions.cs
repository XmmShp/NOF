using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddNOFHosting()
        {
            builder.Services.TryAddSingleton(builder.Environment);
            builder.Services.AddNOFHosting();
            return builder;
        }
    }
}
