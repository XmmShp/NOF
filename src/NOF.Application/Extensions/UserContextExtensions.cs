using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NOF.Application;

public static partial class NOFApplicationExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUserContext(Func<IServiceProvider, IUserContext> implementationFactory)
        {
            ArgumentNullException.ThrowIfNull(implementationFactory);
            services.TryAddScoped<IUserContext>(implementationFactory);
            return services;
        }
    }
}
