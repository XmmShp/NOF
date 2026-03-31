using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;

namespace NOF.Infrastructure;

/// <summary>
/// Registers core framework services including invocation context, command sender,
/// notification publisher, handler resolvers, and handler endpoint name map.
/// </summary>
public class CoreServicesRegistrationStep : IBaseSettingsServiceRegistrationStep<CoreServicesRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.TryAddSingleton<ICacheSerializer, JsonCacheSerializer>();
        builder.Services.TryAddSingleton<ICacheLockRetryStrategy, ExponentialBackoffCacheLockRetryStrategy>();
        builder.Services.TryAddSingleton<ICacheServiceFactory, CacheServiceFactory>();

        builder.Services.TryAddSingleton<IMapper, ManualMapper>();

        if (builder.Services.FirstOrDefault(sd => sd.ServiceType == typeof(IIdGenerator)) is null)
        {
            builder.Services.AddOptions<SnowflakeIdGeneratorOptions>();
            builder.Services.TryAddSingleton<IIdGenerator, SnowflakeIdGenerator>();
        }

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
        builder.Services.TryAddScoped<IEventPublisher, EventPublisher>();
        builder.Services.TryAddSingleton<ICommandHandlerResolver, CommandHandlerResolver>();
        builder.Services.TryAddSingleton<IRequestHandlerResolver, RequestHandlerResolver>();

        builder.Services.AddScoped(sp => sp.GetRequiredKeyedService<ICacheService>(ICacheServiceFactory.DefaultName));
        builder.Services.AddScoped<IDistributedCache>(sp => sp.GetRequiredService<ICacheService>());
        builder.Services.AddScoped<IInvocationContext>(sp => sp.GetRequiredService<IMutableInvocationContext>());
        builder.Services.AddScoped<IUserContext>(sp => sp.GetRequiredService<IMutableUserContext>());
        builder.Services.AddHostedService<OutboxMessageBackgroundService>();
        builder.Services.AddHostedService<RpcOperationStartupValidatorHostedService>();
        builder.Services.AddOptions<OutboxOptions>();
        return ValueTask.CompletedTask;
    }
}
