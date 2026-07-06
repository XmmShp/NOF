using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;
using System.Diagnostics.CodeAnalysis;

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

        public IServiceCollection ReplaceOrAdd(ServiceDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(descriptor);

            var existingDescriptor = services.FirstOrDefault(d => d.ServiceType == descriptor.ServiceType);
            if (existingDescriptor is not null)
            {
                services.Remove(existingDescriptor);
            }

            services.Add(descriptor);
            return services;
        }

        public IServiceCollection ReplaceOrAddSingleton<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
            where TService : class
            where TImplementation : class, TService
            => services.ReplaceOrAdd(ServiceDescriptor.Singleton<TService, TImplementation>());

        public IServiceCollection ReplaceOrAddSingleton<TService>(Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(implementationFactory);
            return services.ReplaceOrAdd(ServiceDescriptor.Singleton(implementationFactory));
        }

        public IServiceCollection ReplaceOrAddSingleton<TService>(TService implementationInstance)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(implementationInstance);
            return services.ReplaceOrAdd(ServiceDescriptor.Singleton(implementationInstance));
        }

        public IServiceCollection ReplaceOrAddScoped<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
            where TService : class
            where TImplementation : class, TService
            => services.ReplaceOrAdd(ServiceDescriptor.Scoped<TService, TImplementation>());

        public IServiceCollection ReplaceOrAddScoped<TService>(Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(implementationFactory);
            return services.ReplaceOrAdd(ServiceDescriptor.Scoped(implementationFactory));
        }

        public IServiceCollection ReplaceOrAddTransient<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
            where TService : class
            where TImplementation : class, TService
            => services.ReplaceOrAdd(ServiceDescriptor.Transient<TService, TImplementation>());

        public IServiceCollection ReplaceOrAddTransient<TService>(Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(implementationFactory);
            return services.ReplaceOrAdd(ServiceDescriptor.Transient(implementationFactory));
        }

        public InitializedTypes InitializedTypes
            => services.GetOrAddSingleton<InitializedTypes>();
    }
}
