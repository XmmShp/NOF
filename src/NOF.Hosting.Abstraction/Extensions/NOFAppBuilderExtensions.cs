using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Contract;

namespace NOF.Hosting;

public static partial class NOFAppBuilderExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddHostingDefaults()
        {
            builder.Services.TryAddSingleton<OutboundPipelineTypes>();
            builder.Services.TryAddSingleton<IOutboundPipelineExecutor, OutboundPipelineExecutor>();
            builder.Services.TryAddScoped<IExecutionContext, Contract.ExecutionContext>();
            builder.Services.TryAddTransient(typeof(Lazy<>), typeof(NOFLazy<>));

            builder.TryAddRegistrationStep(
                       new OutboundMiddlewareRegistrationStep<MessageIdOutboundMiddleware>(),
                       [.. DependencyNode<IServiceRegistrationStep>.CollectRelatedTypes<MessageIdOutboundMiddleware>()])
                   .TryAddRegistrationStep(
                       new OutboundMiddlewareRegistrationStep<TracingOutboundMiddleware>(),
                       [.. DependencyNode<IServiceRegistrationStep>.CollectRelatedTypes<TracingOutboundMiddleware>()])
                   .TryAddRegistrationStep(
                       new OutboundMiddlewareRegistrationStep<TenantOutboundMiddleware>(),
                       [.. DependencyNode<IServiceRegistrationStep>.CollectRelatedTypes<TenantOutboundMiddleware>()]);
            return builder;
        }
    }
}
