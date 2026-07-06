using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;
using NOF.Domain;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class NOFDomainExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddNOFDomain()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddNOFAbstraction();
            services.TryAddSingleton<IIdGenerator, SnowflakeIdGenerator>();
            services.TryAddEnumerable(new ServiceDescriptor(
                typeof(IDaemonService),
                typeof(IdGeneratorAmbientDaemonService),
                ServiceLifetime.Scoped));
            return services;
        }
    }
}
