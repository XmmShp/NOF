using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Hosting;

namespace NOF.Infrastructure;

public static partial class NOFInfrastructureExtensions
{
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Applies the built-in NOF infrastructure defaults (service registration steps and initialization steps).
        /// Each default step is added only when a step of the same type is not already present.
        /// </summary>
        public INOFAppBuilder AddInfrastructureDefaults()
        {
            builder.AddHostingDefaults();
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
            builder.Services.TryAddSingleton<IInboundPipelineExecutor, InboundPipelineExecutor>();
            builder.Services.TryAddSingleton<IStateMachineRegistry, StateMachineRegistry>();
            builder.Services.TryAddScoped<IDeferredCommandSender, DeferredCommandSender>();
            builder.Services.TryAddScoped<IDeferredNotificationPublisher, DeferredNotificationPublisher>();
            builder.Services.TryAddScoped<ICommandSender, CommandSender>();
            builder.Services.TryAddScoped<INotificationPublisher, NotificationPublisher>();
            builder.Services.TryAddScoped<IEventPublisher, EventPublisher>();
            builder.Services.AddScoped(sp => sp.GetRequiredKeyedService<ICacheService>(ICacheServiceFactory.DefaultName));
            builder.Services.AddScoped<IDistributedCache>(sp => sp.GetRequiredService<ICacheService>());
            builder.Services.TryAddScoped<IUserContext, UserContext>();
            builder.Services.AddHostedService<OutboxMessageBackgroundService>();
            builder.Services.AddOptions<OutboxOptions>();

            builder.TryAddRegistrationStep<OpenTelemetryRegistrationStep>()
                .TryAddRegistrationStep<ExceptionInboundMiddlewareStep>()
                .TryAddRegistrationStep<TenantInboundMiddlewareStep>()
                .TryAddRegistrationStep<AuthorizationInboundMiddlewareStep>()
                .TryAddRegistrationStep<TracingInboundMiddlewareStep>()
                .TryAddRegistrationStep<AutoInstrumentationInboundMiddlewareStep>()
                .TryAddRegistrationStep<MessageInboxInboundMiddlewareStep>()
                .TryAddRegistrationStep<RequestHandlerServiceRegistrationStep>()
                .TryAddRegistrationStep<HandlerServiceRegistrationStep>()
                .TryAddInitializationStep<IdGeneratorInitializationStep>()
                .TryAddInitializationStep<MapperInitializationStep>();

            return builder;
        }

        /// <summary>
        /// Uses classic single-tenant mode (default).
        /// </summary>
        public INOFAppBuilder UseSingleTenant(string? tenantId = null)
        {
            builder.Services.Configure<TenantOptions>(options =>
            {
                options.Mode = TenantMode.SingleTenant;
                options.SingleTenantId = NOFContractConstants.Tenant.NormalizeTenantId(tenantId);
            });
            return builder;
        }

        /// <summary>
        /// Uses shared-database multi-tenant mode with a TenantId discriminator column.
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
        /// Uses database-per-tenant mode on the same DB instance.
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

        /// <summary>
        /// Adds a service configuration delegate that will be executed during the service registration phase.
        /// </summary>
        public INOFAppBuilder AddRegistrationStep(Func<IServiceRegistrationContext, ValueTask> func)
            => builder.AddRegistrationStep(new ServiceRegistrationStep(func));

        /// <summary>
        /// Adds an application configuration delegate that will be executed after the host is built but before it starts.
        /// </summary>
        public INOFAppBuilder AddInitializationStep(Func<IHostApplicationBuilder, IHost, Task> func)
            => builder.AddInitializationStep(new ApplicationInitializationStep(func));
    }
}
