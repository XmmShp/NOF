using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NOF.Abstraction;
using NOF.Application;
using NOF.Domain;
using NOF.Hosting;

namespace NOF.Infrastructure;

public static partial class NOFInfrastructureExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddInfrastructureDefaults()
        {
            #region Core Services
            builder.Services.TryAddSingleton<ICacheLockRetryStrategy, ExponentialBackoffCacheLockRetryStrategy>();
            builder.Services.GetOrAddSingleton<MapperInfos>();
            builder.Services.GetOrAddSingleton<CommandHandlerInfos>();
            builder.Services.GetOrAddSingleton<NotificationHandlerInfos>();
            builder.Services.GetOrAddSingleton<RequestHandlerInfos>();
            builder.Services.GetOrAddSingleton<RpcServerInfos>();
            builder.Services.TryAddSingleton<IMapper, ManualMapper>();
            builder.Services.TryAddSingleton<IObjectSerializer, JsonObjectSerializer>();
            builder.Services.TryAddSingleton<IIdGenerator, SnowflakeIdGenerator>();
            builder.Services.TryAddSingleton<InboxMessageStore>();

            builder.Services.TryAddScoped<ICacheService, CacheService>();
            builder.Services.TryAddScoped<IDistributedCache>(sp => sp.GetRequiredService<ICacheService>());

            builder.Services.TryAddScoped<NOFDbContext>(sp => sp.GetRequiredService<INOFDbContextFactory>().CreateDbContext());
            builder.Services.TryAddScoped<DbContext>(sp => sp.GetRequiredService<NOFDbContext>());

            #endregion

            #region Options
            builder.Services.AddOptions<CacheServiceOptions>();
            builder.Services.AddOptions<SnowflakeIdGeneratorOptions>();
            builder.Services.AddOptions<DbContextConfigurationOptions>();
            builder.Services.AddOptions<TransactionalMessageOptions>();
            #endregion

            #region Background Services
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxMessageBackgroundService>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, InboxMessageBackgroundService>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, InboxCleanupBackgroundService>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxCleanupBackgroundService>());
            #endregion

            #region Pipelines
            builder.Services.GetOrAddSingleton<CommandOutboundPipelineTypes>();
            builder.Services.GetOrAddSingleton<NotificationOutboundPipelineTypes>();
            builder.Services.GetOrAddSingleton<CommandInboundPipelineTypes>();
            builder.Services.GetOrAddSingleton<NotificationInboundPipelineTypes>();
            builder.Services.GetOrAddSingleton<RequestInboundPipelineTypes>();
            builder.Services.TryAddScoped<CommandOutboundPipelineExecutor>();
            builder.Services.TryAddScoped<NotificationOutboundPipelineExecutor>();
            builder.Services.TryAddSingleton<CommandInboundPipelineExecutor>();
            builder.Services.TryAddSingleton<NotificationInboundPipelineExecutor>();
            builder.Services.TryAddSingleton<RequestInboundPipelineExecutor>();
            #endregion

            #region Application Services
            builder.Services.TryAddSingleton<IStateMachineRegistry, StateMachineRegistry>();
            builder.Services.GetOrAddSingleton<EventHandlerInfos>();
            builder.Services.TryAddScoped<IExecutionContext, Application.ExecutionContext>();
            builder.Services.TryAddScoped<ICommandSender, CommandSender>();
            builder.Services.TryAddScoped<INotificationPublisher, NotificationPublisher>();
            builder.Services.TryAddScoped<IEventPublisher, InMemoryEventPublisher>();
            #endregion

            #region Outbound Middlewares
            builder.Services.AddCommandOutboundMiddleware<ExecutionContextHeadersOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<ExecutionContextHeadersOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<ExecutionContextHeadersOutboundMiddleware>();
            builder.Services.AddCommandOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddCommandOutboundMiddleware<TracingOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<TracingOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<TracingOutboundMiddleware>();
            #endregion

            #region Inbound Middlewares
            builder.Services.AddCommandInboundMiddleware<DaemonServiceInboundMiddleware>();
            builder.Services.AddNotificationInboundMiddleware<DaemonServiceInboundMiddleware>();
            builder.Services.AddRequestInboundMiddleware<DaemonServiceInboundMiddleware>();
            builder.Services.AddCommandInboundMiddleware<InboundExceptionMiddleware>();
            builder.Services.AddNotificationInboundMiddleware<InboundExceptionMiddleware>();
            builder.Services.AddRequestInboundMiddleware<InboundExceptionMiddleware>();
            builder.Services.AddCommandInboundMiddleware<TenantInboundMiddleware>();
            builder.Services.AddNotificationInboundMiddleware<TenantInboundMiddleware>();
            builder.Services.AddRequestInboundMiddleware<TenantInboundMiddleware>();
            builder.Services.AddRequestInboundMiddleware<AuthorizationInboundMiddleware>();
            builder.Services.AddCommandInboundMiddleware<TracingInboundMiddleware>();
            builder.Services.AddNotificationInboundMiddleware<TracingInboundMiddleware>();
            builder.Services.AddRequestInboundMiddleware<TracingInboundMiddleware>();
            builder.Services.AddCommandInboundMiddleware<AutoInstrumentationInboundMiddleware>();
            builder.Services.AddNotificationInboundMiddleware<AutoInstrumentationInboundMiddleware>();
            builder.Services.AddRequestInboundMiddleware<AutoInstrumentationInboundMiddleware>();
            #endregion

            #region Registration & Initialization Steps
            builder.TryAddRegistrationStep<OpenTelemetryRegistrationStep>()
                .TryAddRegistrationStep<RequestHandlerServiceRegistrationStep>()
                .TryAddRegistrationStep<HandlerServiceRegistrationStep>()
                .TryAddInitializationStep<IdGeneratorInitializationStep>()
                .TryAddInitializationStep<MapperInitializationStep>();
            #endregion

            #region Default Persistence
            builder.Services.TryAddScoped<ICacheServiceRider, MemoryCacheServiceRider>();
            builder.Services.TryAddSingleton<ICommandRider, MemoryCommandRider>();
            builder.Services.TryAddSingleton<INotificationRider, MemoryNotificationRider>();
            builder.Services.TryAddSingleton<SqliteInMemoryConnectionKeeper>();


            builder.UseDbContext<NOFDbContext>()
                .WithConnectionString("Data Source=nof-sqlite-memory-{tenantId};Mode=Memory;Cache=Shared")
                .WithTenantMode(TenantMode.DatabasePerTenant)
                .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseSqlite(connectionString));

            #endregion

            return builder;
        }

        public EFCoreSelector UseDbContext<TDbContext>()
            where TDbContext : NOFDbContext
        {
            builder.Services.ReplaceOrAddScoped<INOFDbContextFactory<TDbContext>, NOFDbContextFactory<TDbContext>>();
            builder.Services.ReplaceOrAddScoped<IDbContextFactory<TDbContext>, DbContextFactory<TDbContext>>();
            builder.Services.ReplaceOrAddScoped<INOFDbContextFactory>(sp => sp.GetRequiredService<INOFDbContextFactory<TDbContext>>());
            if (typeof(TDbContext) != typeof(NOFDbContext))
            {
                builder.Services.ReplaceOrAddScoped<TDbContext>(sp => sp.GetRequiredService<INOFDbContextFactory<TDbContext>>().CreateDbContext());
            }

            return new EFCoreSelector(builder);
        }

        public INOFAppBuilder AddRegistrationStep(Func<IServiceRegistrationContext, ValueTask> func)
            => builder.AddRegistrationStep(new ServiceRegistrationStep(func));

        public INOFAppBuilder AddInitializationStep(Func<IHost, Task> func)
            => builder.AddInitializationStep(new ApplicationInitializationStep(func));
    }
}
