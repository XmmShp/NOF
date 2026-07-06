using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;
using NOF.Application;
using NOF.Domain;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class NOFApplicationExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddNOFApplication()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddNOFAbstraction();
            services.AddNOFDomain();
            services.GetOrAddSingleton<MapperRegistry>();
            services.GetOrAddSingleton<CommandHandlerRegistry>();
            services.GetOrAddSingleton<NotificationHandlerRegistry>();
            services.GetOrAddSingleton<RpcServerRegistry>();
            services.TryAddSingleton<IMapper, ManualMapper>();
            services.TryAddSingleton<IStateMachineRegistry, StateMachineRegistry>();
            services.TryAddEnumerable(new ServiceDescriptor(
                typeof(IDaemonService),
                typeof(MapperAmbientDaemonService),
                ServiceLifetime.Scoped));
            return services;
        }
    }
}
