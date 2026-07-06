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
using NOF.Domain;
using NOF.Hosting;
using NOF.Infrastructure.EntityFrameworkCore;
using System.Diagnostics;
using Xunit;

namespace NOF.Infrastructure.Tests.Steps;

public class NOFInfrastructureTests
{
    [Fact]
    public void AddNOFInfrastructure_ShouldNotRegisterPersistenceServicesByDefault()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        builder.AddNOFInfrastructure();

        using var provider = BuildServiceProvider(builder);
        using var scope = provider.CreateScope();
        Assert.Null(scope.ServiceProvider.GetService<Microsoft.EntityFrameworkCore.DbContext>());
        Assert.Null(scope.ServiceProvider.GetService<IDbContext>());
        Assert.IsType<CacheService>(scope.ServiceProvider.GetRequiredService<ICacheService>());
        Assert.IsType<MemoryCacheServiceRider>(scope.ServiceProvider.GetRequiredService<ICacheServiceRider>());
    }

    [Fact]
    public void AddNOFInfrastructure_ShouldRegisterCoreServicesAndOutboxOptions()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddNOFInfrastructure();

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
        Assert.NotNull(provider.GetRequiredService<IOptions<TransactionalMessageOptions>>());
        Assert.Equal(TenantMode.DatabasePerTenant, provider.GetRequiredService<IOptions<DbContextConfigurationOptions>>().Value.TenantMode);
        Assert.IsType<MemoryBackplane>(provider.GetRequiredService<IBackplane>());
        Assert.IsType<InMemoryEventPublisher>(provider.GetRequiredService<IEventPublisher>());
        Assert.IsType<HttpAuthorizationServerService>(provider.GetRequiredService<IClientCredentialsTokenService>());
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IRequestOutboundMiddleware) &&
            service.ImplementationType == typeof(ServiceTokenOutboundMiddleware));
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(ICommandOutboundMiddleware) &&
            service.ImplementationType == typeof(ServiceTokenOutboundMiddleware));
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(INotificationOutboundMiddleware) &&
            service.ImplementationType == typeof(ServiceTokenOutboundMiddleware));
    }

    [Fact]
    public void AddNOFInfrastructure_ShouldRegisterAmbientMapperAndIdGeneratorDaemons()
    {
        var builder = new TestServiceRegistrationContext();

        builder.AddNOFInfrastructure();

        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IDaemonService) &&
            service.ImplementationType == typeof(MapperAmbientDaemonService));
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IDaemonService) &&
            service.ImplementationType == typeof(IdGeneratorAmbientDaemonService));
    }

    [Fact]
    public void AddNOFInfrastructure_ShouldRegisterCurrentTenantAsScoped()
    {
        var builder = new TestServiceRegistrationContext();

        builder.AddNOFInfrastructure();

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
        builder.AddNOFInfrastructure();
        builder.UseDbContext<NOFDbContext>().WithTenantMode(TenantMode.SharedDatabase);

        using var provider = BuildServiceProvider(builder);
        var options = provider.GetRequiredService<IOptions<DbContextConfigurationOptions>>().Value;
        Assert.Equal(TenantMode.SharedDatabase, options.TenantMode);
    }

    [Fact]
    public void AddNOFInfrastructure_ShouldUseDatabasePerTenantByDefault()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddNOFInfrastructure();

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
    public void AddNOFInfrastructure_ShouldValidateSnowflakeOptions()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.Configure<SnowflakeIdGeneratorOptions>(options => options.ApplicationIdBits = 0);
        builder.AddNOFInfrastructure();

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

    private sealed class TestServiceRegistrationContext : IHostApplicationBuilder
    {
        private readonly IServiceCollection _services;
        private readonly ConfigurationManager _configuration;
        private readonly IHostEnvironment _environment;
        private readonly ILoggingBuilder _logging;
        private readonly IMetricsBuilder _metrics;
        private readonly Dictionary<object, object> _properties;
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
        }

        public TestServiceRegistrationContext(TestServiceRegistrationContext other)
        {
            _services = other._services;
            _configuration = other._configuration;
            _environment = other._environment;
            _logging = other._logging;
            _metrics = other._metrics;
            _properties = other._properties;
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
        if (!builder.Services.Any(descriptor => descriptor.ServiceType == typeof(IHostEnvironment)))
        {
            builder.Services.AddSingleton(builder.Environment);
        }

        if (!builder.Services.Any(descriptor => descriptor.ServiceType == typeof(IConfiguration)))
        {
            builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        }

        return builder.Services.BuildServiceProvider();
    }

}
