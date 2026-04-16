using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NOF.Abstraction;
using Xunit;

namespace NOF.Hosting.Abstraction.Tests;

public class DaemonServiceScopeTests
{
    [Fact]
    public void CreateScope_ShouldMaterializeDaemonServices_PerScopeOnly()
    {
        ScopeDaemon.Reset();
        var services = new ServiceCollection();
        services.AddScoped<IDaemonService, ScopeDaemon>();

        using var provider = services.BuildNOFServiceProvider();
        Assert.Equal(0, ScopeDaemon.CreatedCount);

        using var firstScope = provider.CreateScope();
        Assert.Equal(1, ScopeDaemon.CreatedCount);

        using var secondScope = provider.CreateScope();
        Assert.Equal(2, ScopeDaemon.CreatedCount);
    }

    [Fact]
    public void AddHostingDefaults_ShouldRegisterAmbientEventPublisherDaemon()
    {
        var builder = new TestAppBuilder();

        builder.AddHostingDefaults();

        Assert.Contains(builder.Services, service =>
            service.ServiceType == typeof(IDaemonService)
            && service.ImplementationType == typeof(EventPublisherAmbientDaemonService));

        using var provider = builder.Services.BuildNOFServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEventPublisher>());
    }

    private sealed class ScopeDaemon : IDaemonService
    {
        private static int _createdCount;

        public ScopeDaemon()
        {
            Interlocked.Increment(ref _createdCount);
        }

        public static int CreatedCount => _createdCount;

        public static void Reset()
        {
            Interlocked.Exchange(ref _createdCount, 0);
        }
    }

    private sealed class TestAppBuilder : NOFAppBuilder<FakeHost>
    {
        private readonly ServiceCollection _services = [];
        private readonly ConfigurationManager _configuration = new();
        private readonly TestHostEnvironment _environment = new();
        private readonly Dictionary<object, object> _properties = [];
        private readonly ServiceCollection _loggingServices = [];
        private readonly ServiceCollection _metricsServices = [];

        public override IDictionary<object, object> Properties => _properties;
        public override IConfigurationManager Configuration => _configuration;
        public override IHostEnvironment Environment => _environment;
        public override ILoggingBuilder Logging => new TestLoggingBuilder(_loggingServices);
        public override IMetricsBuilder Metrics => new TestMetricsBuilder(_metricsServices);
        public override IServiceCollection Services => _services;

        protected override Task<FakeHost> BuildApplicationAsync()
            => throw new NotSupportedException();

        public override void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
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

        public string ApplicationName { get; set; } = "NOF.Hosting.Abstraction.Tests";

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
