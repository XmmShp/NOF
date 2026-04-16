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
            builder.Services.TryAddSingleton<ICacheLockRetryStrategy, ExponentialBackoffCacheLockRetryStrategy>();
            builder.Services.GetOrAddSingleton<MapperInfos>();
            builder.Services.GetOrAddSingleton<CommandHandlerInfos>();
            builder.Services.GetOrAddSingleton<NotificationHandlerInfos>();
            builder.Services.GetOrAddSingleton<RequestHandlerInfos>();
            builder.Services.GetOrAddSingleton<RpcServerInfos>();
            builder.Services.TryAddSingleton<IMapper, ManualMapper>();

            if (builder.Services.FirstOrDefault(sd => sd.ServiceType == typeof(IIdGenerator)) is null)
            {
                builder.Services.AddOptions<SnowflakeIdGeneratorOptions>();
                builder.Services.TryAddSingleton<IIdGenerator, SnowflakeIdGenerator>();
            }

            builder.Services.TryAddSingleton<IObjectSerializer, JsonObjectSerializer>();
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
            builder.Services.TryAddSingleton<IStateMachineRegistry, StateMachineRegistry>();
            builder.Services.GetOrAddSingleton<EventHandlerInfos>();
            builder.Services.TryAddScoped<IExecutionContext, Application.ExecutionContext>();
            builder.Services.TryAddScoped<ICommandSender, CommandSender>();
            builder.Services.TryAddScoped<INotificationPublisher, NotificationPublisher>();
            builder.Services.TryAddScoped<IEventPublisher, InMemoryEventPublisher>();
            builder.Services.AddHostedService<OutboxMessageBackgroundService>();
            builder.Services.AddOptions<OutboxOptions>();

            builder.Services.AddCommandOutboundMiddleware<ExecutionContextHeadersOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<ExecutionContextHeadersOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<ExecutionContextHeadersOutboundMiddleware>();
            builder.Services.AddCommandOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddCommandOutboundMiddleware<TracingOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<TracingOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<TracingOutboundMiddleware>();

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

            builder.TryAddRegistrationStep<OpenTelemetryRegistrationStep>()
                .TryAddRegistrationStep<RequestHandlerServiceRegistrationStep>()
                .TryAddRegistrationStep<HandlerServiceRegistrationStep>()
                .TryAddInitializationStep<IdGeneratorInitializationStep>()
                .TryAddInitializationStep<MapperInitializationStep>();

            if (!builder.Services.Any(sd => sd.ServiceType == typeof(DbContext))
                && !builder.Services.Any(sd => sd.ServiceType == typeof(IDbContextConfigurator)))
            {
                builder.AddMemoryInfrastructure();
            }

            return builder;
        }

        public INOFAppBuilder UseSingleTenant(string? tenantId = null)
        {
            builder.Services.Configure<TenantOptions>(options =>
            {
                options.Mode = TenantMode.SingleTenant;
                options.SingleTenantId = NOFAbstractionConstants.Tenant.NormalizeTenantId(tenantId);
            });
            return builder;
        }

        public INOFAppBuilder UseSharedDatabaseTenancy()
        {
            builder.Services.Configure<TenantOptions>(options =>
            {
                options.Mode = TenantMode.SharedDatabase;
            });
            return builder;
        }

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
