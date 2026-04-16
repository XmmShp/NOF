using Microsoft.Extensions.DependencyInjection;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    extension(IServiceCollection services)
    {
        public NOFServiceProvider BuildNOFServiceProvider(ServiceProviderOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var inner = options is null ? services.BuildServiceProvider() : services.BuildServiceProvider(options);
            return new NOFServiceProvider(inner);
        }
    }
}
