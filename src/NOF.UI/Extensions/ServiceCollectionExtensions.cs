using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.UI;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class NOFUIExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddNOFUI()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddScoped<IBrowserInfoService, BrowserInfoService>();
            services.TryAddScoped<ILocalStorage, LocalStorage>();
            services.TryAddScoped<ISessionStorage, SessionStorage>();
            return services;
        }
    }
}
