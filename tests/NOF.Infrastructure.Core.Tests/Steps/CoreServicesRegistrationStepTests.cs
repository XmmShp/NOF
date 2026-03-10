using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NOF.Application;
using NOF.Domain;
using NOF.Infrastructure.Abstraction;
using NOF.Infrastructure.Core;
using Xunit;

namespace NOF.Infrastructure.Core.Tests.Steps;

public class CoreServicesRegistrationStepTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldRegisterDefaultInMemoryPersistenceServices()
    {
        var builder = new TestServiceRegistrationContext();
        var context = new TestServiceRegistrationContext(builder);
        var step = new CoreServicesRegistrationStep();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        await step.ExecuteAsync(context);

        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<InMemoryPersistenceStore>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<InMemoryPersistenceSession>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Should().BeOfType<InMemoryUnitOfWork>();
        scope.ServiceProvider.GetRequiredService<ITransactionManager>().Should().BeOfType<InMemoryTransactionManager>();
        scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>().Should().BeOfType<InMemoryInboxMessageRepository>();
        scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>().Should().BeOfType<InMemoryOutboxMessageRepository>();
        scope.ServiceProvider.GetRequiredService<ITenantRepository>().Should().BeOfType<InMemoryTenantRepository>();
        scope.ServiceProvider.GetRequiredService<IStateMachineContextRepository>().Should().BeOfType<InMemoryStateMachineContextRepository>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRegisterMemoryPersistenceWarningHostedService()
    {
        var builder = new TestServiceRegistrationContext();
        var context = new TestServiceRegistrationContext(builder);
        var step = new CoreServicesRegistrationStep();

        await step.ExecuteAsync(context);

        using var provider = builder.Services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>().ToList();

        hostedServices.Should().Contain(service => service is MemoryPersistenceWarningHostedService);
    }

    private sealed class TestServiceRegistrationContext : IServiceRegistrationContext
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

        public IServiceRegistrationContext AddInitializationStep(IApplicationInitializationStep initializationStep)
            => this;

        public IServiceRegistrationContext RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
            => this;

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

        public string ApplicationName { get; set; } = "NOF.Infrastructure.Core.Tests";

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
