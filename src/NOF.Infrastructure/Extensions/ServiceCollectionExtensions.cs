using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Application;
using NOF.Contract;
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

        /// <summary>
        /// Registers an inbound middleware type and appends it to <see cref="InboundPipelineTypes"/>.
        /// The final execution order is resolved when the pipeline freezes.
        /// </summary>
        public IServiceCollection AddInboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, IInboundMiddleware
        {
            services.TryAddScoped<TMiddleware>();
            services.GetOrAddSingleton<InboundPipelineTypes>().Add<TMiddleware>();
            return services;
        }

        /// <summary>
        /// Marker method for split-interface RPC service registration.
        /// The actual registrations are produced by the Infrastructure source generator
        /// via interceptors, similar to how Hosting intercepts endpoint mapping calls.
        /// </summary>
        /// <typeparam name="TService">The RPC service interface</typeparam>
        /// <typeparam name="TSplitedInterface">The split interface type implementing ISplitedInterface&lt;TService&gt;</typeparam>
        public IServiceCollection AddSplitInterfaceService<TService, TSplitedInterface>()
            where TService : class, IRpcService
            where TSplitedInterface : class, ISplitedInterface<TService>
        {
            return services;
        }
    }
}
