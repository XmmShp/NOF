using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

// ReSharper disable once InconsistentNaming
public static partial class NOFInfrastructureExtensions
{
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCommandInboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, ICommandInboundMiddleware
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped<ICommandInboundMiddleware, TMiddleware>());
            return services;
        }

        public IServiceCollection AddCommandOutboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, ICommandOutboundMiddleware
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped<ICommandOutboundMiddleware, TMiddleware>());
            return services;
        }

        public IServiceCollection AddNotificationInboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, INotificationInboundMiddleware
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped<INotificationInboundMiddleware, TMiddleware>());
            return services;
        }

        public IServiceCollection AddNotificationOutboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, INotificationOutboundMiddleware
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped<INotificationOutboundMiddleware, TMiddleware>());
            return services;
        }

        public IServiceCollection AddRequestInboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, IRequestInboundMiddleware
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped<IRequestInboundMiddleware, TMiddleware>());
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
