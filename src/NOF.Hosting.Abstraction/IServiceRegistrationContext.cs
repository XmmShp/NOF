using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

/// <summary>
/// Context available during service registration steps.
/// </summary>
public interface IServiceRegistrationContext : IHostApplicationBuilder
{
    Registry Registry { get; }
}

public static partial class NOFHostingExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInitializationStep(IApplicationInitializationStep initializationStep)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(initializationStep);

            services.AddSingleton<IApplicationInitializationStep>(initializationStep);
            return services;
        }

        public IServiceCollection AddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IApplicationInitializationStep
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<IApplicationInitializationStep, T>();
            return services;
        }

        public IServiceCollection AddInitializationStep<T>(Func<IServiceProvider, T> factory) where T : class, IApplicationInitializationStep
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(factory);

            services.AddSingleton<IApplicationInitializationStep>(factory);
            return services;
        }

        public IServiceCollection RemoveInitializationStep<T>() where T : class, IApplicationInitializationStep
        {
            ArgumentNullException.ThrowIfNull(services);

            for (var i = services.Count - 1; i >= 0; i--)
            {
                if (MatchesInitializationStepType(services[i], typeof(T)))
                {
                    services.RemoveAt(i);
                }
            }

            return services;
        }

        public IServiceCollection RemoveInitializationStep<T>(Predicate<T> predicate) where T : class, IApplicationInitializationStep
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(predicate);

            for (var i = services.Count - 1; i >= 0; i--)
            {
                var descriptor = services[i];
                if (descriptor.ServiceType != typeof(IApplicationInitializationStep))
                {
                    continue;
                }

                if (descriptor.ImplementationInstance is T step && predicate(step))
                {
                    services.RemoveAt(i);
                }
            }

            return services;
        }

        public IServiceCollection TryAddInitializationStep(IApplicationInitializationStep initializationStep)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(initializationStep);

            return HasInitializationStep(services, initializationStep.GetType())
                ? services
                : services.AddInitializationStep(initializationStep);
        }

        public IServiceCollection TryAddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IApplicationInitializationStep
        {
            ArgumentNullException.ThrowIfNull(services);

            return HasInitializationStep(services, typeof(T))
                ? services
                : services.AddInitializationStep<T>();
        }

        public IServiceCollection TryAddInitializationStep<T>(Func<IServiceProvider, T> factory) where T : class, IApplicationInitializationStep
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(factory);

            return HasInitializationStep(services, typeof(T))
                ? services
                : services.AddInitializationStep(factory);
        }
    }

    private static bool HasInitializationStep(IServiceCollection services, Type implementationType)
    {
        return services.Any(descriptor => MatchesInitializationStepType(descriptor, implementationType));
    }

    private static bool MatchesInitializationStepType(ServiceDescriptor descriptor, Type implementationType)
    {
        if (descriptor.ServiceType != typeof(IApplicationInitializationStep))
        {
            return false;
        }

        if (descriptor.ImplementationType == implementationType)
        {
            return true;
        }

        return descriptor.ImplementationInstance?.GetType() == implementationType;
    }
}
