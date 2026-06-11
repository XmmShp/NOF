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
using System.Diagnostics;
using Xunit;

namespace NOF.Infrastructure.Tests.Steps;

public class InfrastructureDefaultsTests
{
    [Fact]
    public void AddInfrastructureDefaults_ShouldRegisterDefaultMemoryPersistenceServices()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        builder.AddInfrastructureDefaults();

        using var provider = BuildServiceProvider(builder);
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<Microsoft.EntityFrameworkCore.DbContext>());
        Assert.IsType<CacheService>(scope.ServiceProvider.GetRequiredService<ICacheService>());
        Assert.IsType<MemoryCacheServiceRider>(scope.ServiceProvider.GetRequiredService<ICacheServiceRider>());
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
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IDaemonService) &&
            service.ImplementationType == typeof(ContextAmbientDaemonService));
    }

    [Fact]
    public void AddInfrastructureDefaults_ShouldRegisterContextAccessorAsSingleton()
    {
        var builder = new TestServiceRegistrationContext();

        builder.AddInfrastructureDefaults();

        var descriptor = Assert.Single(builder.Services, service => service.ServiceType == typeof(IContextAccessor));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(ContextAccessor), descriptor.ImplementationType);
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

        Assert.Contains(builder.InitializationSteps, step => step is DbContextMigrationInitializationStep);
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
        private readonly List<IApplicationInitializationStep> _initializationSteps;
        private readonly Registry _registry;

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
            _initializationSteps = [];
            _registry = new Registry();
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
            _initializationSteps = other._initializationSteps;
            _registry = other._registry;
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

        public INOFAppBuilder AddInitializationStep(IApplicationInitializationStep initializationStep)
        {
            _initializationSteps.Add(initializationStep);
            return this;
        }

        public INOFAppBuilder RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
        {
            _initializationSteps.RemoveAll(predicate);
            return this;
        }

        IServiceRegistrationContext IServiceRegistrationContext.AddInitializationStep(IApplicationInitializationStep initializationStep)
            => AddInitializationStep(initializationStep);

        IServiceRegistrationContext IServiceRegistrationContext.RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
            => RemoveInitializationStep(predicate);

        public IDictionary<object, object> Properties => _properties;

        public Registry Registry => _registry;

        public IConfigurationManager Configuration => _configuration;

        public IHostEnvironment Environment => _environment;

        public ILoggingBuilder Logging => _logging;

        public IMetricsBuilder Metrics => _metrics;

        public IServiceCollection Services => _services;

        public IReadOnlyList<IApplicationInitializationStep> InitializationSteps => _initializationSteps;

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
        new AutoInjectServiceRegistrationStep().ExecuteAsync(builder).GetAwaiter().GetResult();
        return builder.Services.BuildServiceProvider();
    }

}
