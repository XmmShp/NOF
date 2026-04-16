using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddHostingDefaults()
        {
            builder.Services.TryAddSingleton<CommandOutboundPipelineTypes>();
            builder.Services.TryAddSingleton<NotificationOutboundPipelineTypes>();
            builder.Services.TryAddSingleton<RequestOutboundPipelineTypes>();
            builder.Services.TryAddSingleton<ICommandOutboundPipelineExecutor, CommandOutboundPipelineExecutor>();
            builder.Services.TryAddSingleton<INotificationOutboundPipelineExecutor, NotificationOutboundPipelineExecutor>();
            builder.Services.TryAddSingleton<IRequestOutboundPipelineExecutor, RequestOutboundPipelineExecutor>();
            builder.Services.TryAddScoped<IUserContext, UserContext>();
            builder.Services.TryAddTransient(typeof(Lazy<>), typeof(NOFLazy<>));
            builder.Services.AddCommandOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddCommandOutboundMiddleware<TracingOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<TracingOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<TracingOutboundMiddleware>();
            builder.Services.AddCommandOutboundMiddleware<TenantOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<TenantOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<TenantOutboundMiddleware>();
            return builder;
        }
    }
}
