using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NOF.UI;

public static partial class NOFUIExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddNOFUI()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddScoped<ILocalStorage, LocalStorage>();
            services.TryAddScoped<ISessionStorage, SessionStorage>();
            return services;
        }
    }
}
