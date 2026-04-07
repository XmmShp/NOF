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
    }
}
