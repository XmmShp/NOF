using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Infrastructure;
using Xunit;

namespace NOF.Infrastructure.RabbitMQ.Tests.Services;

public class RabbitMQConsumerHostedServiceTests
{
    [Fact]
    public void BuildNotificationQueueName_ShouldPrefixApplicationName()
    {
        var queueName = RabbitMQConsumerHostedService.BuildNotificationQueueName(
            "Orders.Api",
            "JwtRotationNotificationHandler");

        Assert.Equal("Orders.Api.JwtRotationNotificationHandler", queueName);
    }

    [Fact]
    public void BuildNotificationQueueName_ShouldFallbackToOriginalName_WhenApplicationNameIsEmpty()
    {
        var queueName = RabbitMQConsumerHostedService.BuildNotificationQueueName(
            string.Empty,
            "JwtRotationNotificationHandler");

        Assert.Equal("JwtRotationNotificationHandler", queueName);
    }

    [Fact]
    public async Task StartAsync_ShouldThrow_WhenConsumerInitializationFails()
    {
        var connectionManager = new RabbitMQConnectionManager(Options.Create(new RabbitMQOptions
        {
            HostName = "localhost"
        }));
        connectionManager.Dispose();

        var commandHandlerRegistry = new CommandHandlerRegistry();
        commandHandlerRegistry.Add(new CommandHandlerRegistration(typeof(TestCommandHandler), typeof(TestCommand)));

        var service = new RabbitMQConsumerHostedService(
            connectionManager,
            Options.Create(new RabbitMQOptions()),
            commandHandlerRegistry,
            new NotificationHandlerRegistry(),
            new TestHostEnvironment(),
            null!,
            new TypeResolver(),
            NullLogger<RabbitMQConsumerHostedService>.Instance);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.StartAsync(CancellationToken.None));
    }

    [Fact]
    public void ShouldRequeue_ShouldUseOptionsForUnexpectedFailures()
    {
        var requeue = RabbitMQConsumerHostedService.ShouldRequeue(
            new InvalidOperationException("database unavailable"),
            new RabbitMQOptions());

        Assert.True(requeue);
    }

    [Fact]
    public void ShouldRequeue_ShouldRespectConfiguredConsumerFailurePolicy()
    {
        var requeue = RabbitMQConsumerHostedService.ShouldRequeue(
            new InvalidOperationException("database unavailable"),
            new RabbitMQOptions { RequeueOnConsumerFailure = false });

        Assert.False(requeue);
    }

    [Fact]
    public void ShouldRequeue_ShouldUseMessageExceptionPolicyForPoisonMessages()
    {
        var requeue = RabbitMQConsumerHostedService.ShouldRequeue(
            new RabbitMQConsumerMessageException("missing message type", requeue: false),
            new RabbitMQOptions());

        Assert.False(requeue);
    }

    private sealed class TestCommand;

    private sealed class TestCommandHandler;

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "NOF.Infrastructure.RabbitMQ.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
