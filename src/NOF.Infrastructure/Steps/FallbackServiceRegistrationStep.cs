using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;

namespace NOF.Infrastructure;

public sealed class FallbackServiceRegistrationStep : IBaseSettingsServiceRegistrationStep<FallbackServiceRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.TryAddSingleton<ICacheSerializer, JsonCacheSerializer>();
        builder.Services.TryAddSingleton<ICacheLockRetryStrategy, ExponentialBackoffCacheLockRetryStrategy>();
        builder.Services.TryAddSingleton<ICacheServiceFactory, DefaultCacheServiceFactory>();

        builder.Services.TryAddSingleton<IMapper, ManualMapper>();

        builder.Services.AddOptions<SnowflakeIdGeneratorOptions>();
        builder.Services.TryAddSingleton<IIdGenerator, SnowflakeIdGenerator>();
        builder.Services.TryAddSingleton<IMessageSerializer, JsonMessageSerializer>();

        builder.Services.TryAddScoped<IInboundPipelineExecutor, InboundPipelineExecutor>();
        builder.Services.TryAddScoped<IOutboundPipelineExecutor, OutboundPipelineExecutor>();
        builder.Services.TryAddSingleton<IStateMachineRegistry, StateMachineRegistry>();

        builder.Services.TryAddScoped<IDeferredCommandSender, DeferredCommandSender>();
        builder.Services.TryAddScoped<IDeferredNotificationPublisher, DeferredNotificationPublisher>();
        builder.Services.TryAddScoped<IMutableUserContext, UserContext>();
        builder.Services.TryAddScoped<IMutableInvocationContext, InvocationContext>();
        builder.Services.TryAddScoped<ICommandSender, CommandSender>();
        builder.Services.TryAddScoped<INotificationPublisher, NotificationPublisher>();
        builder.Services.TryAddScoped<IRequestSender, RequestSender>();
        builder.Services.TryAddSingleton<ICommandHandlerResolver, CommandHandlerResolver>();
        builder.Services.TryAddSingleton<IRequestHandlerResolver, RequestHandlerResolver>();

        return ValueTask.CompletedTask;
    }
}
