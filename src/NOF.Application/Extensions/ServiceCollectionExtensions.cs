using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;
using NOF.Application;
using System.Linq.Expressions;

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
            services.GetOrAddSingleton<MappingRegistry>();
            services.GetOrAddSingleton<CommandHandlerRegistry>();
            services.GetOrAddSingleton<NotificationHandlerRegistry>();
            services.GetOrAddSingleton<RpcServerRegistry>();
            services.TryAddSingleton<IMapper, ExpressionMapper>();
            services.TryAddEnumerable(new ServiceDescriptor(
                typeof(IDaemonService),
                typeof(MapperAmbientDaemonService),
                ServiceLifetime.Scoped));
            return services;
        }

        /// <summary>
        /// Adds an explicit expression-based mapping registration.
        /// Register mappings before the first <see cref="IMapper"/> is resolved.
        /// </summary>
        public IServiceCollection AddMapping<TSource, TDestination>(
            Expression<Func<TSource, TDestination>> expression,
            string? name = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(expression);

            services.AddNOFApplication();
            services.GetOrAddSingleton<MappingRegistry>()
                .Add(MappingRegistration.Of(expression, name));
            return services;
        }
    }
}
