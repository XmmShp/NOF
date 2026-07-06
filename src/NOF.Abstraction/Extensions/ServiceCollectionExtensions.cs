using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class NOFAbstractionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddNOFAbstraction()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.GetOrAddSingleton<EventHandlerRegistry>();
            services.TryAddScoped<IUserContext, UserContext>();
            services.TryAddScoped<IEventPublisher, InMemoryEventPublisher>();
            services.TryAddEnumerable(new ServiceDescriptor(
                typeof(IDaemonService),
                typeof(EventPublisherAmbientDaemonService),
                ServiceLifetime.Scoped));
            return services;
        }

        public T GetOrAddSingleton<T>() where T : class, new()
        {
            ArgumentNullException.ThrowIfNull(services);

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(T));
            if (descriptor?.ImplementationInstance is T existing)
            {
                return existing;
            }

            var instance = new T();
            services.AddSingleton(instance);
            return instance;
        }

        public T GetOrAddSingleton<T>(Func<T> factory) where T : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(factory);

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(T));
            if (descriptor?.ImplementationInstance is T existing)
            {
                return existing;
            }

            var instance = factory();
            services.AddSingleton(instance);
            return instance;
        }

        public InitializedTypes InitializedTypes
            => services.GetOrAddSingleton<InitializedTypes>();
    }
}
