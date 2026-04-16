using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Replaces an existing service descriptor or adds a new one if it doesn't exist.
        /// </summary>
        /// <param name="descriptor">The service descriptor to replace or add.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection ReplaceOrAdd(ServiceDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            var existingDescriptor = services.FirstOrDefault(d => d.ServiceType == descriptor.ServiceType);
            if (existingDescriptor is not null)
            {
                services.Remove(existingDescriptor);
            }

            services.Add(descriptor);
            return services;
        }

        /// <summary>
        /// Replaces an existing singleton service or adds a new one if it doesn't exist.
        /// </summary>
        public IServiceCollection ReplaceOrAddSingleton<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
            where TService : class
            where TImplementation : class, TService
            => services.ReplaceOrAdd(ServiceDescriptor.Singleton<TService, TImplementation>());

        /// <summary>
        /// Replaces an existing singleton service or adds a new one if it doesn't exist.
        /// </summary>
        public IServiceCollection ReplaceOrAddSingleton<TService>(Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(implementationFactory);
            return services.ReplaceOrAdd(ServiceDescriptor.Singleton(implementationFactory));
        }

        /// <summary>
        /// Replaces an existing singleton service or adds a new one if it doesn't exist.
        /// </summary>
        public IServiceCollection ReplaceOrAddSingleton<TService>(TService implementationInstance)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(implementationInstance);
            return services.ReplaceOrAdd(ServiceDescriptor.Singleton(implementationInstance));
        }

        /// <summary>
        /// Replaces an existing scoped service or adds a new one if it doesn't exist.
        /// </summary>
        public IServiceCollection ReplaceOrAddScoped<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
            where TService : class
            where TImplementation : class, TService
            => services.ReplaceOrAdd(ServiceDescriptor.Scoped<TService, TImplementation>());

        /// <summary>
        /// Replaces an existing scoped service or adds a new one if it doesn't exist.
        /// </summary>
        public IServiceCollection ReplaceOrAddScoped<TService>(Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(implementationFactory);
            return services.ReplaceOrAdd(ServiceDescriptor.Scoped(implementationFactory));
        }

        /// <summary>
        /// Replaces an existing transient service or adds a new one if it doesn't exist.
        /// </summary>
        public IServiceCollection ReplaceOrAddTransient<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
            where TService : class
            where TImplementation : class, TService
            => services.ReplaceOrAdd(ServiceDescriptor.Transient<TService, TImplementation>());

        /// <summary>
        /// Replaces an existing transient service or adds a new one if it doesn't exist.
        /// </summary>
        public IServiceCollection ReplaceOrAddTransient<TService>(Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(implementationFactory);
            return services.ReplaceOrAdd(ServiceDescriptor.Transient(implementationFactory));
        }

        /// <summary>
        /// Retrieves the singleton instance of <typeparamref name="T"/> already registered in the service collection,
        /// or creates a new instance using the parameterless constructor, registers it, and returns it.
        /// </summary>
        public T GetOrAddSingleton<T>() where T : class, new()
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(T));
            if (descriptor?.ImplementationInstance is T existing)
            {
                return existing;
            }

            var instance = new T();
            services.AddSingleton(instance);
            return instance;
        }

        public IServiceCollection AddRequestOutboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, IRequestOutboundMiddleware
        {
            services.TryAddScoped<TMiddleware>();
            services.GetOrAddSingleton<RequestOutboundPipelineTypes>().Add<TMiddleware>();
            return services;
        }
    }
}
