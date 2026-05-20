using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

// ReSharper disable once InconsistentNaming
public static partial class NOFInfrastructureExtensions
{
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    extension(IServiceCollection services)
    {
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

        /// <summary>
        /// Registers a hosted service backed by an asynchronous delegate.
        /// </summary>
        public IServiceCollection AddHostedService(Func<IServiceProvider, CancellationToken, Task> startAction)
        {
            return services.AddHostedService(sp => new DelegateBackgroundService(sp, startAction));
        }

        /// <summary>
        /// Registers a hosted service backed by a synchronous delegate.
        /// </summary>
        public IServiceCollection AddHostedService(Action<IServiceProvider, CancellationToken> startAction)
            => services.AddHostedService((sp, ct) => { startAction(sp, ct); return Task.CompletedTask; });

    }
}
