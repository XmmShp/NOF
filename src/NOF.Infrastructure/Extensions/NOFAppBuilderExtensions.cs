using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NOF.Abstraction;
using NOF.Application;
using NOF.Domain;
using NOF.Hosting;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

public static partial class NOFInfrastructureExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddInfrastructureDefaults()
        {
            _ = builder.Registry.EventHandlerRegistry;
            _ = builder.Registry.MapperRegistry;
            _ = builder.Registry.CommandHandlerRegistry;
            _ = builder.Registry.NotificationHandlerRegistry;
            _ = builder.Registry.RequestHandlerRegistry;
            _ = builder.Registry.RpcServerRegistry;
            builder.Services.GetOrAddSingleton<TypeResolver>();

            #region Core Services
            builder.Services.TryAddSingleton<ICacheLockRetryStrategy, ExponentialBackoffCacheLockRetryStrategy>();
            builder.Services.TryAddSingleton<IMapper, ManualMapper>();
            builder.Services.TryAddSingleton<IObjectSerializer, JsonObjectSerializer>();
            builder.Services.TryAddSingleton<IIdGenerator>(sp => new SnowflakeIdGenerator(
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SnowflakeIdGeneratorOptions>>().Value,
                builder.Environment.ApplicationId,
                builder.Environment.InstanceId));
            builder.Services.TryAddSingleton<IContextAccessor, ContextAccessor>();
            builder.Services.TryAddSingleton<InboxMessageStore>();
            builder.Services.TryAddScoped<RpcServerInvocationResolver>();
            builder.Environment.BindConfiguration(builder.Configuration);

            builder.Services.TryAddSingleton<MemoryCacheServiceRiderState>();
            builder.Services.TryAddSingleton<CacheServiceLocalLockState>();
            builder.Services.TryAddScoped<ICacheService>(sp => new CacheService(
                sp.GetRequiredService<ICacheServiceRider>(),
                sp.GetRequiredService<IObjectSerializer>(),
                sp.GetRequiredService<ICacheLockRetryStrategy>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CacheServiceOptions>>(),
                sp.GetRequiredService<IContextAccessor>(),
                sp.GetRequiredService<CacheServiceLocalLockState>()));
            builder.Services.TryAddScoped<IDistributedCache>(sp => sp.GetRequiredService<ICacheService>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IRequestAuthorizationPolicy, MetadataRequestAuthorizationPolicy>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IDaemonService, MapperAmbientDaemonService>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IDaemonService, IdGeneratorAmbientDaemonService>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IDaemonService, ContextAmbientDaemonService>());

            builder.Services.TryAddScoped(sp => sp.GetRequiredService<INOFDbContextFactory>().CreateDbContext());
            builder.Services.TryAddScoped<DbContext>(sp => sp.GetRequiredService<NOFDbContext>());

            #endregion

            #region Options
            builder.Services.AddOptions<CacheServiceOptions>();
            builder.Services.AddOptions<SnowflakeIdGeneratorOptions>()
                .Validate(options =>
                    Validator.TryValidateObject(
                        options,
                        new ValidationContext(options),
                        null,
                        validateAllProperties: true),
                    "SnowflakeIdGeneratorOptions is invalid.");
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
            builder.Services.TryAddSingleton<CommandInboundPipelineExecutor>();
            builder.Services.TryAddSingleton<NotificationInboundPipelineExecutor>();
            builder.Services.TryAddSingleton<RequestInboundPipelineExecutor>();
            #endregion

            #region Application Services
            builder.Services.TryAddSingleton<IStateMachineRegistry, StateMachineRegistry>();
            builder.Services.TryAddScoped<ICommandSender, CommandSender>();
            builder.Services.TryAddScoped<INotificationPublisher, NotificationPublisher>();
            builder.Services.TryAddScoped<IEventPublisher, InMemoryEventPublisher>();
            #endregion

            #region Outbound Middlewares
            builder.Services.AddCommandOutboundMiddleware<ContextHeadersOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<ContextHeadersOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<ContextHeadersOutboundMiddleware>();
            builder.Services.AddCommandOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddCommandOutboundMiddleware<TracingOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<TracingOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<TracingOutboundMiddleware>();
            #endregion

            #region Inbound Middlewares
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
                .TryAddRegistrationStep<HandlerServiceRegistrationStep>();
            #endregion

            #region Default Persistence
            builder.Services.TryAddScoped<ICacheServiceRider>(sp => new MemoryCacheServiceRider(
                sp.GetRequiredService<MemoryCacheServiceRiderState>()));
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

        public EFCoreSelector UseDbContext<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TDbContext>()
            where TDbContext : NOFDbContext
        {
            builder.Services.ReplaceOrAddScoped<INOFDbContextFactory<TDbContext>, NOFDbContextFactory<TDbContext>>();
            builder.Services.ReplaceOrAddScoped<IDbContextFactory<TDbContext>, DbContextFactory<TDbContext>>();
            builder.Services.ReplaceOrAddScoped<INOFDbContextFactory>(sp => sp.GetRequiredService<INOFDbContextFactory<TDbContext>>());
            if (typeof(TDbContext) != typeof(NOFDbContext))
            {
                builder.Services.ReplaceOrAddScoped(sp => sp.GetRequiredService<INOFDbContextFactory<TDbContext>>().CreateDbContext());
            }

            return new EFCoreSelector(builder, typeof(TDbContext));
        }

        public INOFAppBuilder AddDbContextModelCreating(Action<ModelBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            builder.Services.AddSingleton<INOFDbContextModelCreatingContributor>(
                new DelegateDbContextModelCreatingContributor(configure));
            return builder;
        }

        public INOFAppBuilder AddRegistrationStep(Func<IServiceRegistrationContext, ValueTask> func)
            => builder.AddRegistrationStep(new ServiceRegistrationStep(func));
    }
}
