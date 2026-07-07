using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    private static readonly object HostEnvironmentConfiguredKey = new();

    extension(IHostApplicationBuilder builder)
    {
        internal IHostApplicationBuilder ConfigureNOFHostEnvironment()
        {
            if (builder.Properties.TryGetValue(HostEnvironmentConfiguredKey, out var configured)
                && configured is true)
            {
                return builder;
            }

            builder.Environment.BindConfiguration(builder.Configuration);
            builder.Services.RemoveAll<IHostEnvironment>();
            builder.Services.AddSingleton(builder.Environment);
            builder.Properties[HostEnvironmentConfiguredKey] = true;
            return builder;
        }
    }
}
