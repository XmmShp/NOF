using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Hosting;
using NOF.Infrastructure.EntityFrameworkCore;
using System.Diagnostics;
using Xunit;

namespace NOF.Infrastructure.Tests.Steps;

public class InfrastructureDefaultsTests
{
    [Fact]
    public void AddInfrastructureDefaults_ShouldNotRegisterPersistenceServicesByDefault()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        builder.AddInfrastructureDefaults();

        using var provider = BuildServiceProvider(builder);
        using var scope = provider.CreateScope();
        Assert.Null(scope.ServiceProvider.GetService<Microsoft.EntityFrameworkCore.DbContext>());
        Assert.Null(scope.ServiceProvider.GetService<IDbContext>());
        Assert.IsType<CacheService>(scope.ServiceProvider.GetRequiredService<ICacheService>());
        Assert.IsType<MemoryCacheServiceRider>(scope.ServiceProvider.GetRequiredService<ICacheServiceRider>());
    }

    [Fact]
    public async Task AddInMemoryPersistence_ShouldRegisterSelectablePersistenceProvider()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddInfrastructureDefaults();
        builder.AddInMemoryPersistence();

        using var provider = BuildServiceProvider(builder);
        using (var scope = provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
            dbContext.Set<NOFOutboxMessage>().Add(new NOFOutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = OutboxMessageType.Command,
                PayloadType = "payload",
                DispatchTypes = "[]",
                Payload = [],
                Headers = "{}"
            });

            Assert.Equal(1, await dbContext.SaveChangesAsync());
        }

        using (var scope = provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
            var tracked = await dbContext.Set<NOFOutboxMessage>().SingleAsync();
            tracked.Status = OutboxMessageStatus.Failed;

            Assert.Equal(1, await dbContext.SaveChangesAsync());
        }

        using (var scope = provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
            var detached = await dbContext.Set<NOFOutboxMessage>().AsNoTracking().SingleAsync();
            detached.Status = OutboxMessageStatus.Sent;

            Assert.Equal(0, await dbContext.SaveChangesAsync());
            Assert.Equal(OutboxMessageStatus.Failed, (await dbContext.Set<NOFOutboxMessage>().SingleAsync()).Status);
        }

        using (var scope = provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
            var updated = await dbContext.Set<NOFOutboxMessage>()
                .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.Status, OutboxMessageStatus.Sent));
            var messages = await dbContext.Set<NOFOutboxMessage>().ToListAsync();

            Assert.Equal(1, updated);
            Assert.Single(messages);
            Assert.Equal(OutboxMessageStatus.Sent, messages[0].Status);

            var deleted = await dbContext.Set<NOFOutboxMessage>().ExecuteDeleteAsync();
            Assert.Equal(1, deleted);
            Assert.False(await dbContext.Set<NOFOutboxMessage>().AnyAsync());
        }
    }

    [Fact]
    public void AddInMemoryPersistence_ShouldRegisterDbContextAsScoped()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddInfrastructureDefaults();
        builder.AddInMemoryPersistence();

        using var provider = BuildServiceProvider(builder);
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var firstResolution = firstScope.ServiceProvider.GetRequiredService<IDbContext>();
        var secondResolution = firstScope.ServiceProvider.GetRequiredService<IDbContext>();
        var otherScopeResolution = secondScope.ServiceProvider.GetRequiredService<IDbContext>();

        Assert.Same(firstResolution, secondResolution);
        Assert.NotSame(firstResolution, otherScopeResolution);
    }

    [Fact]
    public async Task RequestInboundPipeline_ShouldResolveInMemoryEventHandlersFromCurrentScope()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        builder.AddInMemoryPersistence();
        builder.Services.AddScoped<ScopeMarker>();
        builder.Services.AddSingleton<ScopeEventProbe>();
        builder.Services.AddTransient<ScopedRequestHandler>();
        builder.Services.AddTransient<ScopedEventHandler>();
        builder.Services.GetOrAddSingleton<EventHandlerRegistry>()
            .Add(new EventHandlerRegistration(typeof(ScopedEventHandler), typeof(ScopedEvent)));

        using var provider = BuildServiceProvider(builder);
        using var scope = provider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();

        var expectedDbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var expectedMarker = scope.ServiceProvider.GetRequiredService<ScopeMarker>();
        var probe = scope.ServiceProvider.GetRequiredService<ScopeEventProbe>();
        probe.ExpectedDbContext = expectedDbContext;
        probe.ExpectedMarker = expectedMarker;
        var executor = scope.ServiceProvider.GetRequiredService<RequestInboundPipelineExecutor>();

        await executor.ExecuteAsync(
            new ScopedRequest(),
            typeof(ScopedRequestHandler),
            typeof(Result),
            typeof(IScopedRpcService),
            nameof(IScopedRpcService.Check),
            headers: null,
            CancellationToken.None);

        Assert.True(probe.RequestHandlerUsedCurrentDbContext);
        Assert.True(probe.EventHandlerUsedRequestHandlerDbContext);
        Assert.True(probe.EventHandlerUsedRequestScopeMarker);
    }

    [Fact]
    public async Task CommandInboundPipeline_ShouldResolveHandlersAndEventsFromCurrentScope()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        builder.AddInMemoryPersistence();
        builder.Services.AddScoped<ScopeMarker>();
        builder.Services.AddSingleton<ScopeEventProbe>();
        builder.Services.AddTransient<ScopedCommandHandler>();
        builder.Services.AddTransient<ScopedEventHandler>();
        builder.Services.GetOrAddSingleton<EventHandlerRegistry>()
            .Add(new EventHandlerRegistration(typeof(ScopedEventHandler), typeof(ScopedEvent)));

        using var provider = BuildServiceProvider(builder);
        using var scope = provider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();

        var probe = scope.ServiceProvider.GetRequiredService<ScopeEventProbe>();
        probe.ExpectedDbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        probe.ExpectedMarker = scope.ServiceProvider.GetRequiredService<ScopeMarker>();
        var serializer = scope.ServiceProvider.GetRequiredService<IObjectSerializer>();
        var typeResolver = scope.ServiceProvider.GetRequiredService<TypeResolver>();
        var executor = scope.ServiceProvider.GetRequiredService<CommandInboundPipelineExecutor>();

        await executor.ExecuteAsync(
            serializer.Serialize(new ScopedCommand()),
            typeResolver.Register(typeof(ScopedCommand)),
            typeof(ScopedCommandHandler),
            headers: null,
            CancellationToken.None);

        Assert.True(probe.CommandHandlerUsedCurrentDbContext);
        Assert.True(probe.EventHandlerUsedCommandHandlerDbContext);
        Assert.True(probe.EventHandlerUsedCommandScopeMarker);
    }

    [Fact]
    public async Task NotificationInboundPipeline_ShouldResolveHandlersAndEventsFromCurrentScope()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        builder.AddInMemoryPersistence();
        builder.Services.AddScoped<ScopeMarker>();
        builder.Services.AddSingleton<ScopeEventProbe>();
        builder.Services.AddTransient<ScopedNotificationHandler>();
        builder.Services.AddTransient<ScopedEventHandler>();
        builder.Services.GetOrAddSingleton<EventHandlerRegistry>()
            .Add(new EventHandlerRegistration(typeof(ScopedEventHandler), typeof(ScopedEvent)));

        using var provider = BuildServiceProvider(builder);
        using var scope = provider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();

        var probe = scope.ServiceProvider.GetRequiredService<ScopeEventProbe>();
        probe.ExpectedDbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        probe.ExpectedMarker = scope.ServiceProvider.GetRequiredService<ScopeMarker>();
        var serializer = scope.ServiceProvider.GetRequiredService<IObjectSerializer>();
        var typeResolver = scope.ServiceProvider.GetRequiredService<TypeResolver>();
        var executor = scope.ServiceProvider.GetRequiredService<NotificationInboundPipelineExecutor>();

        await executor.ExecuteAsync(
            serializer.Serialize(new ScopedNotification()),
            typeResolver.Register(typeof(ScopedNotification)),
            typeof(ScopedNotificationHandler),
            headers: null,
            CancellationToken.None);

        Assert.True(probe.NotificationHandlerUsedCurrentDbContext);
        Assert.True(probe.EventHandlerUsedNotificationHandlerDbContext);
        Assert.True(probe.EventHandlerUsedNotificationScopeMarker);
    }

    [Fact]
    public void AddInfrastructureDefaults_ShouldRegisterCoreServicesAndOutboxOptions()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddInfrastructureDefaults();

        using var provider = BuildServiceProvider(builder);

        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IHostedService) &&
            service.ImplementationType == typeof(OutboxMessageBackgroundService));
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IHostedService) &&
            service.ImplementationType == typeof(InboxMessageBackgroundService));
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IHostedService) &&
            service.ImplementationType == typeof(InboxCleanupBackgroundService));
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IHostedService) &&
            service.ImplementationType == typeof(OutboxCleanupBackgroundService));
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(CommandInboundPipelineExecutor) &&
            service.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(NotificationInboundPipelineExecutor) &&
            service.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(RequestInboundPipelineExecutor) &&
            service.Lifetime == ServiceLifetime.Scoped);
        Assert.Equal(4, builder.Services.Count(service =>
            service.ServiceType == typeof(IHostedService)));
        Assert.NotNull(

        provider.GetRequiredService<IOptions<TransactionalMessageOptions>>());
        Assert.Equal(TenantMode.DatabasePerTenant,
        provider.GetRequiredService<IOptions<DbContextConfigurationOptions>>().Value.TenantMode);
        Assert.IsType<InMemoryEventPublisher>(provider.GetRequiredService<IEventPublisher>());
    }

    [Fact]
    public void AddInfrastructureDefaults_ShouldRegisterAmbientMapperAndIdGeneratorDaemons()
    {
        var builder = new TestServiceRegistrationContext();

        builder.AddInfrastructureDefaults();

        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IDaemonService) &&
            service.ImplementationType == typeof(MapperAmbientDaemonService));
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IDaemonService) &&
            service.ImplementationType == typeof(IdGeneratorAmbientDaemonService));
    }

    [Fact]
    public void AddInfrastructureDefaults_ShouldRegisterCurrentTenantAsScoped()
    {
        var builder = new TestServiceRegistrationContext();

        builder.AddInfrastructureDefaults();

        var descriptor = Assert.Single(builder.Services, service => service.ServiceType == typeof(ICurrentTenant));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.NotNull(descriptor.ImplementationFactory);
        var mutableDescriptor = Assert.Single(builder.Services, service => service.ServiceType == typeof(IMutableCurrentTenant));
        Assert.Equal(ServiceLifetime.Scoped, mutableDescriptor.Lifetime);
        Assert.NotNull(mutableDescriptor.ImplementationFactory);
        var implementationDescriptor = Assert.Single(builder.Services, service => service.ServiceType == typeof(CurrentTenant));
        Assert.Equal(ServiceLifetime.Scoped, implementationDescriptor.Lifetime);
        Assert.Equal(typeof(CurrentTenant), implementationDescriptor.ImplementationType);
    }

    [Fact]
    public void AddHostedService_WithDelegate_ShouldRegisterDelegateBackgroundService()
    {
        var services = new ServiceCollection();

        services.AddHostedService((_, _) => Task.CompletedTask);

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IHostedService));
        Assert.NotNull(descriptor.ImplementationFactory);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<DelegateBackgroundService>(provider.GetRequiredService<IEnumerable<IHostedService>>().Single());
    }

    [Fact]
    public void UseDbContext_WithTenantMode_ShouldSwitchTenantMode()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddInfrastructureDefaults();
        builder.UseDbContext<NOFDbContext>().WithTenantMode(TenantMode.SharedDatabase);

        using var provider = BuildServiceProvider(builder);
        var options = provider.GetRequiredService<IOptions<DbContextConfigurationOptions>>().Value;
        Assert.Equal(TenantMode.SharedDatabase, options.TenantMode);
    }

    [Fact]
    public void AddInfrastructureDefaults_ShouldUseDatabasePerTenantByDefault()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddInfrastructureDefaults();

        using var provider = BuildServiceProvider(builder);
        var options = provider.GetRequiredService<IOptions<DbContextConfigurationOptions>>().Value;
        Assert.Equal(TenantMode.DatabasePerTenant, options.TenantMode);
    }

    [Fact]
    public void UseDbContext_MigrateOnInitialize_ShouldRegisterDatabaseMigrationStep()
    {
        var builder = new TestServiceRegistrationContext();

        builder.UseDbContext<NOFDbContext>().MigrateOnInitialize();

        Assert.Contains(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(IApplicationInitializationStep)
                && descriptor.ImplementationInstance is DbContextMigrationInitializationStep);
    }

    [Fact]
    public void AddInfrastructureDefaults_ShouldValidateSnowflakeOptions()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.Configure<SnowflakeIdGeneratorOptions>(options => options.ApplicationIdBits = 0);
        builder.AddInfrastructureDefaults();

        using var provider = BuildServiceProvider(builder);
        var act = () => _ = provider.GetRequiredService<IOptions<SnowflakeIdGeneratorOptions>>().Value;
        Assert.Throws<OptionsValidationException>(act);
    }

    [Fact]
    public void HostEnvironmentExtensions_ShouldUseApplicationNameAndDefaultInstanceId()
    {
        var hostEnvironment = new TestHostEnvironment
        {
            ApplicationName = "Orders.Api"
        };

        Assert.Equal(0u, hostEnvironment.ApplicationId);
        Assert.Equal("Orders.Api", hostEnvironment.ApplicationName);
        Assert.Equal(1u, hostEnvironment.InstanceId);
        Assert.True(hostEnvironment.IsPrimaryNodeEnvironment);
    }

    [Fact]
    public void EnvironmentBindConfiguration_ShouldPreferConfiguredValues()
    {
        var hostEnvironment = new TestHostEnvironment
        {
            ApplicationName = "fallback-name",
            ApplicationId = 1,
            InstanceId = 7
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [NOFInfrastructureConstants.Deployment.ConfigurationKeys.ApplicationId] = "42",
                [NOFInfrastructureConstants.Deployment.ConfigurationKeys.InstanceId] = "24"
            })
            .Build();

        hostEnvironment.BindConfiguration(configuration);

        Assert.Equal(42u, hostEnvironment.ApplicationId);
        Assert.Equal("fallback-name", hostEnvironment.ApplicationName);
        Assert.Equal(24u, hostEnvironment.InstanceId);
        Assert.False(hostEnvironment.IsPrimaryNodeEnvironment);
    }

    [Fact]
    public void SetCurrentServiceDeploymentTags_ShouldAttachDeploymentInfoToActivity()
    {
        var hostEnvironment = new TestHostEnvironment
        {
            ApplicationName = "orders-api"
        };
        hostEnvironment.ApplicationId = 42;
        hostEnvironment.InstanceId = 24;

        using var activity = new Activity("deployment-test").Start();
        activity.SetServiceDeploymentTags(hostEnvironment);

        Assert.Equal(42u, activity.GetTagItem(NOFInfrastructureConstants.Deployment.Tags.ApplicationId));
        Assert.Equal("orders-api", activity.GetTagItem(NOFInfrastructureConstants.Deployment.Tags.ApplicationName));
        Assert.Equal(24u, activity.GetTagItem(NOFInfrastructureConstants.Deployment.Tags.InstanceId));
    }

    private sealed class TestServiceRegistrationContext : INOFAppBuilder
    {
        private readonly IServiceCollection _services;
        private readonly ConfigurationManager _configuration;
        private readonly IHostEnvironment _environment;
        private readonly ILoggingBuilder _logging;
        private readonly IMetricsBuilder _metrics;
        private readonly Dictionary<object, object> _properties;
        private readonly List<IServiceRegistrationStep> _registrationSteps;

        public TestServiceRegistrationContext()
        {
            _services = new ServiceCollection();
            _services.AddLogging();
            _services.AddMetrics();
            _configuration = new ConfigurationManager();
            _environment = new TestHostEnvironment();
            _logging = new TestLoggingBuilder(_services);
            _metrics = new TestMetricsBuilder(_services);
            _properties = [];
            _registrationSteps = [];
        }

        public TestServiceRegistrationContext(TestServiceRegistrationContext other)
        {
            _services = other._services;
            _configuration = other._configuration;
            _environment = other._environment;
            _logging = other._logging;
            _metrics = other._metrics;
            _properties = other._properties;
            _registrationSteps = other._registrationSteps;
        }

        public INOFAppBuilder AddRegistrationStep(IServiceRegistrationStep registrationStep)
        {
            _registrationSteps.Add(registrationStep);
            return this;
        }

        public INOFAppBuilder RemoveRegistrationStep(Predicate<IServiceRegistrationStep> predicate)
        {
            _registrationSteps.RemoveAll(predicate);
            return this;
        }

        public IDictionary<object, object> Properties => _properties;

        public IConfigurationManager Configuration => _configuration;

        public IHostEnvironment Environment => _environment;

        public ILoggingBuilder Logging => _logging;

        public IMetricsBuilder Metrics => _metrics;

        public IServiceCollection Services => _services;

        public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
            where TContainerBuilder : notnull
        {
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "NOF.Infrastructure.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestLoggingBuilder(IServiceCollection services) : ILoggingBuilder
    {
        public IServiceCollection Services { get; } = services;
    }

    private sealed class TestMetricsBuilder(IServiceCollection services) : IMetricsBuilder
    {
        public IServiceCollection Services { get; } = services;
    }

    private sealed class NullFileProvider : IFileProvider
    {
        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

        public IFileInfo GetFileInfo(string subpath) => new NotFoundFileInfo(subpath);

        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        private long _current = 1000;

        public long NextId() => Interlocked.Increment(ref _current);
    }

    private static ServiceProvider BuildServiceProvider(TestServiceRegistrationContext builder)
    {
        return builder.Services.BuildServiceProvider();
    }

    private interface IScopedRpcService : IRpcService
    {
        Result Check(ScopedRequest request);
    }

    private sealed record ScopedRequest;

    private sealed class ScopedCommand;

    private sealed class ScopedNotification;

    private sealed record ScopedEvent(IDbContext HandlerDbContext, ScopeMarker HandlerMarker, string Source);

    private sealed class ScopeMarker;

    private sealed class ScopeEventProbe
    {
        public IDbContext? ExpectedDbContext { get; set; }

        public ScopeMarker? ExpectedMarker { get; set; }

        public bool RequestHandlerUsedCurrentDbContext { get; set; }

        public bool EventHandlerUsedRequestHandlerDbContext { get; set; }

        public bool EventHandlerUsedRequestScopeMarker { get; set; }

        public bool CommandHandlerUsedCurrentDbContext { get; set; }

        public bool EventHandlerUsedCommandHandlerDbContext { get; set; }

        public bool EventHandlerUsedCommandScopeMarker { get; set; }

        public bool NotificationHandlerUsedCurrentDbContext { get; set; }

        public bool EventHandlerUsedNotificationHandlerDbContext { get; set; }

        public bool EventHandlerUsedNotificationScopeMarker { get; set; }
    }

    private sealed class ScopedRequestHandler : RpcHandler<ScopedRequest, Result>
    {
        private readonly IDbContext _dbContext;
        private readonly ScopeMarker _marker;
        private readonly ScopeEventProbe _probe;

        public ScopedRequestHandler(IDbContext dbContext, ScopeMarker marker, ScopeEventProbe probe)
        {
            _dbContext = dbContext;
            _marker = marker;
            _probe = probe;
        }

        public override Task<Result> HandleAsync(ScopedRequest request, Context context, CancellationToken cancellationToken)
        {
            _probe.RequestHandlerUsedCurrentDbContext = ReferenceEquals(_probe.ExpectedDbContext, _dbContext);
            new ScopedEvent(_dbContext, _marker, "request").PublishAsEvent();
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class ScopedCommandHandler : CommandHandler<ScopedCommand>
    {
        private readonly IDbContext _dbContext;
        private readonly ScopeMarker _marker;
        private readonly ScopeEventProbe _probe;

        public ScopedCommandHandler(IDbContext dbContext, ScopeMarker marker, ScopeEventProbe probe)
        {
            _dbContext = dbContext;
            _marker = marker;
            _probe = probe;
        }

        public override Task HandleAsync(ScopedCommand command, Context context, CancellationToken cancellationToken)
        {
            _probe.CommandHandlerUsedCurrentDbContext = ReferenceEquals(_probe.ExpectedDbContext, _dbContext);
            new ScopedEvent(_dbContext, _marker, "command").PublishAsEvent();
            return Task.CompletedTask;
        }
    }

    private sealed class ScopedNotificationHandler : NotificationHandler<ScopedNotification>
    {
        private readonly IDbContext _dbContext;
        private readonly ScopeMarker _marker;
        private readonly ScopeEventProbe _probe;

        public ScopedNotificationHandler(IDbContext dbContext, ScopeMarker marker, ScopeEventProbe probe)
        {
            _dbContext = dbContext;
            _marker = marker;
            _probe = probe;
        }

        public override Task HandleAsync(ScopedNotification notification, Context context, CancellationToken cancellationToken)
        {
            _probe.NotificationHandlerUsedCurrentDbContext = ReferenceEquals(_probe.ExpectedDbContext, _dbContext);
            new ScopedEvent(_dbContext, _marker, "notification").PublishAsEvent();
            return Task.CompletedTask;
        }
    }

    private sealed class ScopedEventHandler : InMemoryEventHandler<ScopedEvent>
    {
        private readonly IDbContext _dbContext;
        private readonly ScopeMarker _marker;
        private readonly ScopeEventProbe _probe;

        public ScopedEventHandler(IDbContext dbContext, ScopeMarker marker, ScopeEventProbe probe)
        {
            _dbContext = dbContext;
            _marker = marker;
            _probe = probe;
        }

        public override Task HandleAsync(ScopedEvent @event, CancellationToken cancellationToken)
        {
            switch (@event.Source)
            {
                case "request":
                    _probe.EventHandlerUsedRequestHandlerDbContext = ReferenceEquals(@event.HandlerDbContext, _dbContext);
                    _probe.EventHandlerUsedRequestScopeMarker = ReferenceEquals(@event.HandlerMarker, _marker);
                    break;
                case "command":
                    _probe.EventHandlerUsedCommandHandlerDbContext = ReferenceEquals(@event.HandlerDbContext, _dbContext);
                    _probe.EventHandlerUsedCommandScopeMarker = ReferenceEquals(@event.HandlerMarker, _marker);
                    break;
                case "notification":
                    _probe.EventHandlerUsedNotificationHandlerDbContext = ReferenceEquals(@event.HandlerDbContext, _dbContext);
                    _probe.EventHandlerUsedNotificationScopeMarker = ReferenceEquals(@event.HandlerMarker, _marker);
                    break;
            }

            return Task.CompletedTask;
        }
    }

}
