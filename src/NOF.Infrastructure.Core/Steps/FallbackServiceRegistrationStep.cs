using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

public sealed class FallbackServiceRegistrationStep : IBaseSettingsServiceRegistrationStep<FallbackServiceRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.TryAddSingleton<ICacheSerializer, JsonCacheSerializer>();
        builder.Services.TryAddSingleton<ICacheLockRetryStrategy, ExponentialBackoffCacheLockRetryStrategy>();
        builder.Services.TryAddScoped<ICacheService>(sp => sp.GetRequiredKeyedService<ICacheService>(ICacheServiceFactory.DefaultName));
        builder.Services.TryAddScoped<IDistributedCache>(sp => sp.GetRequiredService<ICacheService>());
        builder.Services.TryAddSingleton<ICacheServiceFactory, DefaultCacheServiceFactory>();
        builder.Services.TryAddCacheService<InMemoryCacheService>();

        builder.Services.TryAddSingleton<IMapper, ManualMapper>();

        builder.Services.AddOptions<SnowflakeIdGeneratorOptions>();
        builder.Services.TryAddSingleton<IIdGenerator, SnowflakeIdGenerator>();

        builder.Services.TryAddScoped<IEventPublisher, InMemoryEventPublisher>();
        builder.Services.TryAddSingleton<InMemoryPersistenceStore>();
        builder.Services.TryAddScoped<InMemoryPersistenceSession>();
        builder.Services.TryAddScoped<IUnitOfWork, InMemoryUnitOfWork>();
        builder.Services.TryAddScoped<ITransactionManager, InMemoryTransactionManager>();
        builder.Services.TryAddScoped<IInboxMessageRepository, InMemoryInboxMessageRepository>();
        builder.Services.TryAddScoped<IOutboxMessageRepository, InMemoryOutboxMessageRepository>();
        builder.Services.TryAddScoped<ITenantRepository, InMemoryTenantRepository>();
        builder.Services.TryAddScoped<IStateMachineContextRepository, InMemoryStateMachineContextRepository>();
        builder.Services.TryAddScoped<ICommandRider, InMemoryCommandRider>();
        builder.Services.TryAddScoped<INotificationRider, InMemoryNotificationRider>();
        builder.Services.TryAddScoped<IRequestRider, InMemoryRequestRider>();
        builder.Services.TryAddSingleton<IMessageSerializer, JsonMessageSerializer>();

        builder.Services.TryAddScoped<IInboundPipelineExecutor, InboundPipelineExecutor>();
        builder.Services.TryAddScoped<IOutboundPipelineExecutor, OutboundPipelineExecutor>();
        builder.Services.TryAddSingleton<IStateMachineRegistry, StateMachineRegistry>();

        builder.Services.TryAddScoped<IDeferredCommandSender, DeferredCommandSender>();
        builder.Services.TryAddScoped<IDeferredNotificationPublisher, DeferredNotificationPublisher>();
        builder.Services.TryAddScoped<IMutableUserContext, UserContext>();
        builder.Services.TryAddScoped<IUserContext>(sp => sp.GetRequiredService<IMutableUserContext>());
        builder.Services.TryAddScoped<IMutableInvocationContext, InvocationContext>();
        builder.Services.TryAddScoped<IInvocationContext>(sp => sp.GetRequiredService<IMutableInvocationContext>());
        builder.Services.TryAddScoped<ICommandSender, CommandSender>();
        builder.Services.TryAddScoped<INotificationPublisher, NotificationPublisher>();
        builder.Services.TryAddScoped<IRequestSender, RequestSender>();
        builder.Services.TryAddSingleton<ICommandHandlerResolver, CommandHandlerResolver>();
        builder.Services.TryAddSingleton<IRequestHandlerResolver, RequestHandlerResolver>();

        return ValueTask.CompletedTask;
    }
}
