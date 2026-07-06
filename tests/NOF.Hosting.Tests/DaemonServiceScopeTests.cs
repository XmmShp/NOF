using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NOF.Abstraction;
using Xunit;

namespace NOF.Hosting.Tests;

public class DaemonServiceScopeTests
{
    [Fact]
    public async Task AddNOFHosting_OnServices_ShouldRegisterAmbientEventPublisherDaemon()
    {
        var services = new ServiceCollection();

        services.AddNOFHosting();

        Assert.Contains(services, service =>
            service.ServiceType == typeof(IDaemonService)
            && service.ImplementationType == typeof(EventPublisherAmbientDaemonService));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEventPublisher>());
    }

    [Fact]
    public void AddNOFHosting_OnServices_ShouldBeIdempotent()
    {
        var services = new ServiceCollection();

        services.AddNOFHosting();
        services.AddNOFHosting();

        _ = Assert.Single(services, service => service.ServiceType == typeof(IUserContext));
        _ = Assert.Single(services, service => service.ServiceType == typeof(IEventPublisher));
        _ = Assert.Single(services, service => service.ServiceType == typeof(RequestOutboundPipelineExecutor));
        _ = Assert.Single(services, service =>
            service.ServiceType == typeof(IDaemonService)
            && service.ImplementationType == typeof(EventPublisherAmbientDaemonService));
    }

    [Fact]
    public void AddNOFHosting_OnBuilder_ShouldRegisterEnvironmentAndRemainIdempotent()
    {
        var builder = new TestAppBuilder();

        builder.AddNOFHosting();
        builder.AddNOFHosting();

        _ = Assert.Single(builder.Services, service => service.ServiceType == typeof(IHostEnvironment));
        _ = Assert.Single(builder.Services, service => service.ServiceType == typeof(IUserContext));
        _ = Assert.Single(builder.Services, service => service.ServiceType == typeof(IEventPublisher));
        _ = Assert.Single(builder.Services, service => service.ServiceType == typeof(RequestOutboundPipelineExecutor));
        _ = Assert.Single(builder.Services, service =>
            service.ServiceType == typeof(IDaemonService)
            && service.ImplementationType == typeof(EventPublisherAmbientDaemonService));
    }

    private sealed class TestAppBuilder : IHostApplicationBuilder
    {
        private readonly ServiceCollection _services = [];
        private readonly ConfigurationManager _configuration = new();
        private readonly TestHostEnvironment _environment = new();
        private readonly Dictionary<object, object> _properties = [];
        private readonly ServiceCollection _loggingServices = [];
        private readonly ServiceCollection _metricsServices = [];

        public IDictionary<object, object> Properties => _properties;
        public IConfigurationManager Configuration => _configuration;
        public IHostEnvironment Environment => _environment;
        public ILoggingBuilder Logging => new TestLoggingBuilder(_loggingServices);
        public IMetricsBuilder Metrics => new TestMetricsBuilder(_metricsServices);
        public IServiceCollection Services => _services;

        public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
            where TContainerBuilder : notnull
        {
        }
    }

    private sealed class FakeHost : IHost
    {
        public IServiceProvider Services => throw new NotSupportedException();

        public void Dispose()
        {
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "NOF.Hosting.Tests";

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
}
