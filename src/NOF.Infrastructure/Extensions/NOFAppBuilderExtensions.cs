using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NOF.Abstraction;
using NOF.Application;
using NOF.Domain;
using NOF.Infrastructure;
using System.Diagnostics.CodeAnalysis;
namespace NOF.Hosting;

public static partial class NOFInfrastructureExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddRpcServer<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRpcServer>()
            where TRpcServer : RpcServer, IRpcServer
        {
            builder.Services.TryAddScoped<TRpcServer>();

            var registry = builder.Services.GetOrAddSingleton<RpcServerRegistry>();
            registry.RemoveWhere(existing => existing.ServiceType == TRpcServer.ServiceType);
            registry.Add(new RpcServerRegistration(TRpcServer.ServiceType, typeof(TRpcServer)));
            return builder;
        }

        public INOFAppBuilder AddNOFInfrastructure()
        {
            JwtPropagationRegistrationHooks.Register(AddInfrastructureJwtPropagation);
            builder.Services.AddNOFApplication();
            builder.Services.GetOrAddSingleton<TypeResolver>();

            #region Core Services
            builder.Services.TryAddSingleton<ICacheLockRetryStrategy, ExponentialBackoffCacheLockRetryStrategy>();
            builder.Services.TryAddSingleton<IObjectSerializer, JsonObjectSerializer>();
            builder.Services.AddHttpClient<HttpAuthorizationServerService>();
            builder.Services.TryAddScoped<IClientCredentialsTokenService>(static serviceProvider =>
                serviceProvider.GetRequiredService<HttpAuthorizationServerService>());
            builder.Services.Replace(ServiceDescriptor.Singleton<IIdGenerator>(sp => new SnowflakeIdGenerator(
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SnowflakeIdGeneratorOptions>>().Value,
                builder.Environment.ApplicationId,
                builder.Environment.InstanceId)));
            builder.Services.TryAddScoped<CurrentTenant>();
            builder.Services.TryAddScoped<ICurrentTenant>(static sp => sp.GetRequiredService<CurrentTenant>());
            builder.Services.TryAddScoped<IMutableCurrentTenant>(static sp => sp.GetRequiredService<CurrentTenant>());
            builder.Services.TryAddScoped<RpcServerInvocationResolver>();
            builder.Environment.BindConfiguration(builder.Configuration);

            builder.Services.TryAddSingleton<MemoryCacheServiceRiderState>();
            builder.Services.TryAddSingleton<CacheServiceLocalLockState>();
            builder.Services.TryAddSingleton<MemoryBackplaneState>();
            builder.Services.TryAddScoped<ICacheService>(sp => new CacheService(
                sp.GetRequiredService<ICacheServiceRider>(),
                sp.GetRequiredService<IObjectSerializer>(),
                sp.GetRequiredService<ICacheLockRetryStrategy>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CacheServiceOptions>>(),
                sp.GetRequiredService<ICurrentTenant>(),
                sp.GetRequiredService<CacheServiceLocalLockState>()));
            builder.Services.TryAddScoped<IDistributedCache>(sp => sp.GetRequiredService<ICacheService>());
            builder.Services.TryAddSingleton<IBackplane>(sp => new MemoryBackplane(
                sp.GetRequiredService<MemoryBackplaneState>()));
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, NOFTenantModelCreatingContributor>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, NOFInboxMessageModelCreatingContributor>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, NOFOutboxMessageModelCreatingContributor>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, NOFStateMachineContextModelCreatingContributor>());

            #endregion

            #region Options
            builder.Services.AddOptions<CacheServiceOptions>();
            builder.Services.AddOptions<AuthenticationResourceServerOptions>();
            builder.Services.AddOptions<SnowflakeIdGeneratorOptions>()
                .Validate(static options => options.ApplicationIdBits > 0, "ApplicationIdBits must be greater than zero.")
                .Validate(static options => options.InstanceIdBits > 0, "InstanceIdBits must be greater than zero.")
                .Validate(static options => options.SequenceBits > 0, "SequenceBits must be greater than zero.")
                .Validate(
                    static options => options.ApplicationIdBits + options.InstanceIdBits + options.SequenceBits <= 22,
                    "The sum of ApplicationIdBits, InstanceIdBits, and SequenceBits must be less than or equal to 22.");
            builder.Services.AddOptions<TransactionalMessageOptions>();
            #endregion

            #region Background Services
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxMessageBackgroundService>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, InboxMessageBackgroundService>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, InboxCleanupBackgroundService>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxCleanupBackgroundService>());
            #endregion

            #region Pipelines
            builder.Services.TryAddScoped<CommandInboundPipelineExecutor>();
            builder.Services.TryAddScoped<NotificationInboundPipelineExecutor>();
            builder.Services.TryAddScoped<RequestInboundPipelineExecutor>();
            #endregion

            #region Application Services
            builder.Services.TryAddScoped<ICommandSender, CommandSender>();
            builder.Services.TryAddScoped<INotificationPublisher, NotificationPublisher>();
            #endregion

            #region Outbound Middlewares
            builder.Services.AddCommandOutboundMiddleware<TenantHeaderOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<TenantHeaderOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<TenantHeaderOutboundMiddleware>();
            builder.Services.AddCommandOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<MessageIdOutboundMiddleware>();
            builder.Services.AddCommandOutboundMiddleware<TracingOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<TracingOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<TracingOutboundMiddleware>();
            builder.Services.AddCommandOutboundMiddleware<ServiceTokenOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<ServiceTokenOutboundMiddleware>();
            builder.Services.AddRequestOutboundMiddleware<ServiceTokenOutboundMiddleware>();
            #endregion

            #region Inbound Middlewares
            builder.Services.AddCommandInboundMiddleware<TenantInboundMiddleware>();
            builder.Services.AddNotificationInboundMiddleware<TenantInboundMiddleware>();
            builder.Services.AddRequestInboundMiddleware<TenantInboundMiddleware>();
            builder.Services.AddRequestInboundMiddleware<AuthorizationInboundMiddleware>();
            builder.Services.AddCommandInboundMiddleware<AuthorizationInboundMiddleware>();
            builder.Services.AddNotificationInboundMiddleware<AuthorizationInboundMiddleware>();
            builder.Services.AddCommandInboundMiddleware<TracingInboundMiddleware>();
            builder.Services.AddNotificationInboundMiddleware<TracingInboundMiddleware>();
            builder.Services.AddRequestInboundMiddleware<TracingInboundMiddleware>();
            builder.Services.AddCommandInboundMiddleware<AutoInstrumentationInboundMiddleware>();
            builder.Services.AddNotificationInboundMiddleware<AutoInstrumentationInboundMiddleware>();
            builder.Services.AddRequestInboundMiddleware<AutoInstrumentationInboundMiddleware>();
            #endregion

            #region Registration & Initialization Steps
            builder.TryAddRegistrationStep<OpenTelemetryRegistrationStep>()
                .TryAddRegistrationStep<HandlerServiceRegistrationStep>();
            #endregion

            #region Default Persistence
            builder.Services.TryAddScoped<ICacheServiceRider>(sp => new MemoryCacheServiceRider(
                sp.GetRequiredService<MemoryCacheServiceRiderState>()));
            builder.Services.TryAddSingleton<ICommandRider, MemoryCommandRider>();
            builder.Services.TryAddSingleton<INotificationRider, MemoryNotificationRider>();
            #endregion

            return builder;
        }

        public INOFAppBuilder AddRegistrationStep(Func<IHostApplicationBuilder, ValueTask> func)
            => builder.AddRegistrationStep(new ServiceRegistrationStep(func));

        [RequiresDynamicCode("The in-memory persistence provider exposes LINQ IQueryable over in-memory collections and is intended for tests/development, not Native AOT.")]
        [RequiresUnreferencedCode("The in-memory persistence provider snapshots arbitrary entity types via reflection and is intended for tests/development, not trimmed applications.")]
        public INOFAppBuilder AddInMemoryPersistence()
        {
            builder.Services.ReplaceOrAddSingleton<InMemoryPersistenceStore, InMemoryPersistenceStore>();
            builder.Services.ReplaceOrAddScoped<IDbContext, InMemoryDbContext>();
            return builder;
        }
    }

    private static void AddInfrastructureJwtPropagation(INOFAppBuilder builder)
    {
        builder.Services.AddHttpClient();
        builder.Services.AddHttpClient<HttpAuthorizationServerService>();
        builder.Services.TryAddScoped<IJwtTokenExchangeService>(static serviceProvider =>
            serviceProvider.GetRequiredService<HttpAuthorizationServerService>());
        builder.Services.TryAddScoped<IClientCredentialsTokenService>(static serviceProvider =>
            serviceProvider.GetRequiredService<HttpAuthorizationServerService>());
        builder.Services.AddCommandOutboundMiddleware<Infrastructure.JwtTokenPropagationOutboundMiddleware>();
        builder.Services.AddNotificationOutboundMiddleware<Infrastructure.JwtTokenPropagationOutboundMiddleware>();
    }
}
