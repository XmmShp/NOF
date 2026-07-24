using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class NOFHostingExtensions
{
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    extension(IServiceCollection services)
    {
        public IServiceCollection AddNOFHosting()
        {
            services.AddNOFAbstraction();
            services.TryAddScoped<RequestOutboundPipelineExecutor>();
            services.TryAddTransient(typeof(Lazy<>), typeof(NOFLazy<>));
            return services;
        }

        public IServiceCollection AddRequestOutboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, IRequestOutboundMiddleware
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped<IRequestOutboundMiddleware, TMiddleware>());
            return services;
        }
    }
}
