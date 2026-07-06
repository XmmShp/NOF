using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NOF.Abstraction;
using NOF.Application;
using NOF.Hosting;
using Xunit;

namespace NOF.Infrastructure.RabbitMQ.Tests.Services;

public sealed class RabbitMQBackplaneTests
{
    [Fact]
    public void BuildExchangeName_ShouldUseDedicatedBackplanePrefix()
    {
        var exchangeName = RabbitMQBackplane.BuildExchangeName("chat-stream");

        Assert.Equal("nof.backplane.chat-stream", exchangeName);
    }

    [Fact]
    public void AddRabbitMQBackplane_ShouldRegisterBackplaneWithoutConsumerHostedService()
    {
        var builder = new TestAppBuilder();

        builder.AddRabbitMQBackplane(options => options.ConnectionString = "amqp://guest:guest@localhost:5672/");

        Assert.Contains(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(IBackplane) &&
                          descriptor.ImplementationType == typeof(RabbitMQBackplane));
        Assert.Contains(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(RabbitMQConnectionManager) &&
                          descriptor.ImplementationType == typeof(RabbitMQConnectionManager));
        Assert.DoesNotContain(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                          descriptor.ImplementationType == typeof(RabbitMQConsumerHostedService));
    }

    private sealed class TestAppBuilder : INOFAppBuilder
    {
        private readonly IServiceCollection _services = new ServiceCollection();
        private readonly ConfigurationManager _configuration = new();
        private readonly TestHostEnvironment _environment = new();
        private readonly Dictionary<object, object> _properties = [];
        private readonly ILoggingBuilder _logging;
        private readonly IMetricsBuilder _metrics;

        public TestAppBuilder()
        {
            _services.AddLogging();
            _services.AddMetrics();
            _logging = new TestLoggingBuilder(_services);
            _metrics = new TestMetricsBuilder(_services);
        }

        public IDictionary<object, object> Properties => _properties;

        public IConfigurationManager Configuration => _configuration;

        public IHostEnvironment Environment => _environment;

        public ILoggingBuilder Logging => _logging;

        public IMetricsBuilder Metrics => _metrics;

        public IServiceCollection Services => _services;

        public void ConfigureContainer<TContainerBuilder>(
            IServiceProviderFactory<TContainerBuilder> factory,
            Action<TContainerBuilder>? configure = null)
            where TContainerBuilder : notnull
        {
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "NOF.Infrastructure.RabbitMQ.Tests";

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
