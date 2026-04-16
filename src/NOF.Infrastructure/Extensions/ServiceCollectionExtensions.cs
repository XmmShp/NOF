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
        /// Adds one or more <see cref="HandlerInfo"/> entries to the <see cref="HandlerInfos"/> singleton.
        /// Each entry is dispatched to the appropriate typed set via pattern matching.
        /// Keyed service registrations are deferred to <c>HandlerKeyedServiceRegistrationStep</c>.
        /// </summary>
        public IServiceCollection AddHandlerInfo(params HandlerInfo[] infos)
        {
            var set = services.GetOrAddSingleton<HandlerInfos>();
            foreach (var info in infos)
            {
                set.Add(info);
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

        public IServiceCollection AddNotificationInboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, INotificationInboundMiddleware
        {
            services.TryAddScoped<TMiddleware>();
            services.GetOrAddSingleton<NotificationInboundPipelineTypes>().Add<TMiddleware>();
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
