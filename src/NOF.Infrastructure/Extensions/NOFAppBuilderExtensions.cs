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
            builder.Services.TryAddScoped(sp => (IDistributedCache)sp.GetRequiredService<ICacheService>());
            builder.Services.TryAddScoped(sp => sp.GetRequiredService<INOFDbContextFactory>().CreateDbContext());
            builder.Services.TryAddScoped<DbContext>(sp => sp.GetRequiredService<NOFDbContext>());
            #endregion

            #region Options
            builder.Services.AddOptions<CacheServiceOptions>();
            builder.Services.AddOptions<SnowflakeIdGeneratorOptions>();
            builder.Services.AddOptions<TenantOptions>();
            builder.Services.AddOptions<DbContextConfigurationOptions>();
            builder.Services.AddOptions<OutboxOptions>();
            #endregion

            #region Background Services
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxMessageBackgroundService>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, InboxCleanupBackgroundService>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxCleanupBackgroundService>());
            #endregion

            #region Pipelines
            builder.Services.TryAddSingleton<CommandOutboundPipelineTypes>();
            builder.Services.TryAddSingleton<NotificationOutboundPipelineTypes>();
            builder.Services.TryAddSingleton<CommandInboundPipelineTypes>();
            builder.Services.TryAddSingleton<NotificationInboundPipelineTypes>();
            builder.Services.TryAddSingleton<RequestInboundPipelineTypes>();
            builder.Services.TryAddSingleton<ICommandOutboundPipelineExecutor, CommandOutboundPipelineExecutor>();
            builder.Services.TryAddSingleton<INotificationOutboundPipelineExecutor, NotificationOutboundPipelineExecutor>();
            builder.Services.TryAddSingleton<ICommandInboundPipelineExecutor, CommandInboundPipelineExecutor>();
            builder.Services.TryAddSingleton<INotificationInboundPipelineExecutor, NotificationInboundPipelineExecutor>();
            builder.Services.TryAddSingleton<IRequestInboundPipelineExecutor, RequestInboundPipelineExecutor>();
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
            builder.Services.AddCommandInboundMiddleware<MessageInboxInboundMiddleware>();
            builder.Services.AddNotificationInboundMiddleware<MessageInboxInboundMiddleware>();
            #endregion

            #region Registration & Initialization Steps
            builder.TryAddRegistrationStep<OpenTelemetryRegistrationStep>()
                .TryAddRegistrationStep<RequestHandlerServiceRegistrationStep>()
                .TryAddRegistrationStep<HandlerServiceRegistrationStep>()
                .TryAddInitializationStep<IdGeneratorInitializationStep>()
                .TryAddInitializationStep<MapperInitializationStep>();
            #endregion

            #region Default Persistence
            builder.Services.TryAddScoped<ICacheService, MemoryCacheService>();
            builder.Services.TryAddSingleton<ICommandRider, MemoryCommandRider>();
            builder.Services.TryAddSingleton<INotificationRider, MemoryNotificationRider>();
            builder.Services.TryAddSingleton<SqliteInMemoryConnectionKeeper>();

            builder.UseDbContext<NOFDbContext>()
                .WithConnectionString("Data Source=nof-sqlite-memory-{tenantId};Mode=Memory;Cache=Shared")
                .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseSqlite(connectionString))
                .AutoMigrate();
            #endregion

            return builder;
        }

        public EFCoreSelector UseDbContext<TDbContext>()
            where TDbContext : NOFDbContext
        {
            builder.Services.ReplaceOrAddScoped<INOFDbContextFactory<TDbContext>, NOFDbContextFactory<TDbContext>>();
            builder.Services.ReplaceOrAddScoped<IDbContextFactory<TDbContext>, DbContextFactory<TDbContext>>();
            builder.Services.ReplaceOrAddScoped(sp => (INOFDbContextFactory)sp.GetRequiredService<INOFDbContextFactory<TDbContext>>());
            builder.Services.ReplaceOrAddScoped(sp => sp.GetRequiredService<INOFDbContextFactory<TDbContext>>().CreateDbContext());

            return new EFCoreSelector(builder);
        }

        /// <summary>
        /// Configures application-wide single-tenant mode.
        /// The configured tenant id is used by both storage and cache isolation.
        /// </summary>
        public INOFAppBuilder UseSingleTenant(string? tenantId = null)
        {
            builder.Services.Configure<TenantOptions>(options =>
            {
                options.Mode = TenantMode.SingleTenant;
                options.SingleTenantId = TenantId.Normalize(tenantId);
            });
            return builder;
        }

        /// <summary>
        /// Configures application-wide multi-tenant mode with shared storage and tenant-aware cache isolation.
        /// </summary>
        public INOFAppBuilder UseSharedDatabaseTenancy()
        {
            builder.Services.Configure<TenantOptions>(options =>
            {
                options.Mode = TenantMode.SharedDatabase;
            });
            return builder;
        }

        /// <summary>
        /// Configures application-wide multi-tenant mode with database-per-tenant storage and tenant-aware cache isolation.
        /// </summary>
        public INOFAppBuilder UseDatabasePerTenant(string? tenantDatabaseNameFormat = null)
        {
            builder.Services.Configure<TenantOptions>(options =>
            {
                options.Mode = TenantMode.DatabasePerTenant;
                if (!string.IsNullOrWhiteSpace(tenantDatabaseNameFormat))
                {
                    options.TenantDatabaseNameFormat = tenantDatabaseNameFormat;
                }
            });
            return builder;
        }

        public INOFAppBuilder AddRegistrationStep(Func<IServiceRegistrationContext, ValueTask> func)
            => builder.AddRegistrationStep(new ServiceRegistrationStep(func));

        public INOFAppBuilder AddInitializationStep(Func<IHost, Task> func)
            => builder.AddInitializationStep(new ApplicationInitializationStep(func));
    }
}
