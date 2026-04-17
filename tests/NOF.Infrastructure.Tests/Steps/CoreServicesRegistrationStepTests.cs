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

        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<Microsoft.EntityFrameworkCore.DbContext>());
        Assert.IsType<MemoryCacheService>(scope.ServiceProvider.GetRequiredService<ICacheService>());
    }

    [Fact]
    public void AddInfrastructureDefaults_ShouldRegisterCoreServicesAndOutboxOptions()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddInfrastructureDefaults();

        using var provider = builder.Services.BuildServiceProvider();

        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IHostedService) &&
            service.ImplementationType == typeof(OutboxMessageBackgroundService));
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IHostedService) &&
            service.ImplementationType == typeof(InboxCleanupBackgroundService));
        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IHostedService) &&
            service.ImplementationType == typeof(OutboxCleanupBackgroundService));
        Assert.Equal(3, builder.Services.Count(service =>
            service.ServiceType == typeof(IHostedService)));
        Assert.NotNull(

        provider.GetRequiredService<IOptions<OutboxOptions>>());
        Assert.Equal(TenantMode.SingleTenant,
        provider.GetRequiredService<IOptions<TenantOptions>>().Value.Mode);
        Assert.IsType<InMemoryEventPublisher>(provider.GetRequiredService<IEventPublisher>());

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
    public void UseSharedDatabaseTenancy_ShouldSwitchTenantMode()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddInfrastructureDefaults();
        builder.UseSharedDatabaseTenancy();

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TenantOptions>>().Value;
        Assert.Equal(TenantMode.SharedDatabase,

        options.Mode);
    }

    [Fact]
    public void UseDatabasePerTenant_ShouldSwitchTenantMode_AndApplyFormat()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddInfrastructureDefaults();
        builder.UseDatabasePerTenant("{database}__{tenantId}");

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TenantOptions>>().Value;
        Assert.Equal(TenantMode.DatabasePerTenant,

        options.Mode);
        Assert.Equal("{database}__{tenantId}",
        options.TenantDatabaseNameFormat);
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
        }

        public INOFAppBuilder AddRegistrationStep<TStep>(TStep registrationStep, params Type[] allInterfaces)
            where TStep : IServiceRegistrationStep
        {
            _registrationSteps.Add(registrationStep);
            return this;
        }

        public INOFAppBuilder RemoveRegistrationStep(Predicate<IServiceRegistrationStep> predicate)
        {
            _registrationSteps.RemoveAll(predicate);
            return this;
        }

        public INOFAppBuilder AddInitializationStep<TStep>(TStep initializationStep, params Type[] allInterfaces)
            where TStep : IApplicationInitializationStep
        {
            _initializationSteps.Add(initializationStep);
            return this;
        }

        public INOFAppBuilder RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
        {
            _initializationSteps.RemoveAll(predicate);
            return this;
        }

        IServiceRegistrationContext IServiceRegistrationContext.AddInitializationStep<TStep>(TStep initializationStep, params Type[] allInterfaces)
            => AddInitializationStep(initializationStep, allInterfaces);

        IServiceRegistrationContext IServiceRegistrationContext.RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
            => RemoveInitializationStep(predicate);

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

}
