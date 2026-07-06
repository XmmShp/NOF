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
    public void AddRabbitMQBackplane_OnServices_ShouldRegisterBackplaneWithoutConsumerHostedService()
    {
        var services = new ServiceCollection();

        services.AddRabbitMQBackplane(options => options.ConnectionString = "amqp://guest:guest@localhost:5672/");

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IBackplane) &&
                          descriptor.ImplementationType == typeof(RabbitMQBackplane));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(RabbitMQConnectionManager) &&
                          descriptor.ImplementationType == typeof(RabbitMQConnectionManager));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                          descriptor.ImplementationType == typeof(RabbitMQConsumerHostedService));
    }

}
