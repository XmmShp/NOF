using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Application;
using NOF.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

// ReSharper disable once InconsistentNaming
public static partial class NOFInfrastructureExtensions
{
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds one or more command handler entries to the <see cref="CommandHandlerInfos"/> singleton.
        /// Keyed service registrations are deferred to <c>HandlerKeyedServiceRegistrationStep</c>.
        /// </summary>
        public IServiceCollection AddHandlerRegistration(params CommandHandlerRegistration[] registrations)
        {
            var set = services.GetOrAddSingleton<CommandHandlerInfos>();
            foreach (var registration in registrations)
            {
                set.Add(registration);
            }

            return services;
        }

        /// <summary>
        /// Adds one or more notification handler entries to the <see cref="NotificationHandlerInfos"/> singleton.
        /// Keyed service registrations are deferred to <c>HandlerKeyedServiceRegistrationStep</c>.
        /// </summary>
        public IServiceCollection AddHandlerRegistration(params NotificationHandlerRegistration[] registrations)
        {
            var set = services.GetOrAddSingleton<NotificationHandlerInfos>();
            foreach (var registration in registrations)
            {
                set.Add(registration);
            }

            return services;
        }

        public IServiceCollection AddCommandInboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, ICommandInboundMiddleware
        {
            services.TryAddScoped<TMiddleware>();
            services.GetOrAddSingleton<CommandInboundPipelineTypes>().Add<TMiddleware>();
            return services;
        }

        public IServiceCollection AddCommandOutboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, ICommandOutboundMiddleware
        {
            services.TryAddScoped<TMiddleware>();
            services.GetOrAddSingleton<CommandOutboundPipelineTypes>().Add<TMiddleware>();
            return services;
        }

        public IServiceCollection AddNotificationInboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, INotificationInboundMiddleware
        {
            services.TryAddScoped<TMiddleware>();
            services.GetOrAddSingleton<NotificationInboundPipelineTypes>().Add<TMiddleware>();
            return services;
        }

        public IServiceCollection AddNotificationOutboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, INotificationOutboundMiddleware
        {
            services.TryAddScoped<TMiddleware>();
            services.GetOrAddSingleton<NotificationOutboundPipelineTypes>().Add<TMiddleware>();
            return services;
        }

        public IServiceCollection AddRequestInboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, IRequestInboundMiddleware
        {
            services.TryAddScoped<TMiddleware>();
            services.GetOrAddSingleton<RequestInboundPipelineTypes>().Add<TMiddleware>();
            return services;
        }

    }
}
