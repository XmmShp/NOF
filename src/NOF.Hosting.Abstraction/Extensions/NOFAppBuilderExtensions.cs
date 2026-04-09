using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddHostingDefaults()
        {
            builder.Services.TryAddSingleton<OutboundPipelineTypes>();
            builder.Services.TryAddSingleton<IOutboundPipelineExecutor, OutboundPipelineExecutor>();
            builder.Services.TryAddScoped<IUserContext, UserContext>();
            builder.Services.TryAddScoped<IExecutionContext, ExecutionContext>();
            builder.Services.TryAddTransient(typeof(Lazy<>), typeof(NOFLazy<>));
            builder.Services.AddOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddOutboundMiddleware<TracingOutboundMiddleware>();
            builder.Services.AddOutboundMiddleware<TenantOutboundMiddleware>();
            return builder;
        }
    }
}
