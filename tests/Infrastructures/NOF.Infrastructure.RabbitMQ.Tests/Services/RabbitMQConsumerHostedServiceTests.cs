using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NOF.Application;
using Xunit;

namespace NOF.Infrastructure.RabbitMQ.Tests.Services;

public class RabbitMQConsumerHostedServiceTests
{
    [Fact]
    public void BuildCommandQueueName_ShouldUseCommandRoute()
    {
        var queueName = RabbitMQConsumerHostedService.BuildCommandQueueName(
            "App.Commands.CreateOrderCommand");

        Assert.Equal("App.Commands.CreateOrderCommand", queueName);
    }

    [Fact]
    public void BuildCommandConsumerRegistrations_ShouldUseOneRouteQueueForDuplicateRegistrations()
    {
        var registrations = new[]
        {
            new CommandHandlerRegistration(typeof(TestCommandHandler), typeof(TestCommand)),
            new CommandHandlerRegistration(typeof(TestCommandHandler), typeof(TestCommand))
        };

        var consumer = Assert.Single(
            RabbitMQConsumerHostedService.BuildCommandConsumerRegistrations(registrations));

        Assert.Equal(typeof(TestCommand).DisplayName, consumer.MessageRoute);
        Assert.Equal(typeof(TestCommand).DisplayName, consumer.QueueName);
        Assert.Equal(typeof(TestCommandHandler).DisplayName, consumer.HandlerTypeName);
    }

    [Fact]
    public void BuildCommandConsumerRegistrations_ShouldRejectMultipleHandlersForOneCommand()
    {
        var registrations = new[]
        {
            new CommandHandlerRegistration(typeof(TestCommandHandler), typeof(TestCommand)),
            new CommandHandlerRegistration(typeof(SecondTestCommandHandler), typeof(TestCommand))
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => RabbitMQConsumerHostedService.BuildCommandConsumerRegistrations(registrations));

        Assert.Contains(typeof(TestCommand).DisplayName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildNotificationQueueName_ShouldPrefixServiceName()
    {
        var queueName = RabbitMQConsumerHostedService.BuildNotificationQueueName(
            "Orders.Api",
            "JwtRotationNotificationHandler");

        Assert.Equal("Orders.Api.JwtRotationNotificationHandler", queueName);
    }

    [Fact]
    public void BuildNotificationQueueName_ShouldFallbackToOriginalName_WhenServiceNameIsEmpty()
    {
        var queueName = RabbitMQConsumerHostedService.BuildNotificationQueueName(
            string.Empty,
            "JwtRotationNotificationHandler");

        Assert.Equal("JwtRotationNotificationHandler", queueName);
    }

    [Fact]
    public void BuildNotificationConsumerRegistrations_ShouldCreateOneQueuePerHandler_WhenHandlersShareNotificationType()
    {
        var registrations = new[]
        {
            new NotificationHandlerRegistration(typeof(FirstTestNotificationHandler), typeof(TestNotification)),
            new NotificationHandlerRegistration(typeof(SecondTestNotificationHandler), typeof(TestNotification))
        };

        var consumers = RabbitMQConsumerHostedService.BuildNotificationConsumerRegistrations(
            registrations,
            "Orders.Api");

        Assert.Equal(2, consumers.Count);

        var first = Assert.Single(
            consumers,
            consumer => consumer.HandlerTypeName == typeof(FirstTestNotificationHandler).DisplayName);
        var second = Assert.Single(
            consumers,
            consumer => consumer.HandlerTypeName == typeof(SecondTestNotificationHandler).DisplayName);

        Assert.Equal(
            RabbitMQConsumerHostedService.BuildNotificationQueueName("Orders.Api", typeof(FirstTestNotificationHandler).DisplayName),
            first.QueueName);
        Assert.Equal(
            RabbitMQConsumerHostedService.BuildNotificationQueueName("Orders.Api", typeof(SecondTestNotificationHandler).DisplayName),
            second.QueueName);
        Assert.Equal([typeof(TestNotification)], first.NotificationTypes);
        Assert.Equal([typeof(TestNotification)], second.NotificationTypes);
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
            new JsonObjectSerializer(),
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

    [Fact]
    public void RabbitMQTopology_ShouldUseNamespacedInfrastructureNames()
    {
        Assert.StartsWith("nof.io-vii.com.", RabbitMQTopology.UnroutableExchangeName, StringComparison.Ordinal);
        Assert.StartsWith("nof.io-vii.com.", RabbitMQTopology.UnroutableQueueName, StringComparison.Ordinal);
        Assert.StartsWith("nof.io-vii.com.", RabbitMQTopology.DeadLetterExchangeName, StringComparison.Ordinal);
        Assert.StartsWith("nof.io-vii.com.", RabbitMQTopology.DeadLetterQueueName, StringComparison.Ordinal);
    }

    [Fact]
    public void RabbitMQTopology_ShouldConnectBusinessResourcesToFailureExchanges()
    {
        var exchangeArguments = RabbitMQTopology.BuildBusinessExchangeArguments();
        var queueArguments = RabbitMQTopology.BuildBusinessQueueArguments();

        Assert.Equal(
            RabbitMQTopology.UnroutableExchangeName,
            exchangeArguments["alternate-exchange"]);
        Assert.Equal(
            RabbitMQTopology.DeadLetterExchangeName,
            queueArguments["x-dead-letter-exchange"]);
    }

    [Fact]
    public void RabbitMQTopology_ShouldAddOriginalRouteHeadersForReplay()
    {
        var headers = new Dictionary<string, object?>();

        RabbitMQTopology.AddOriginalRouteHeaders(headers, "App.OrderCreated", string.Empty);

        Assert.Equal("App.OrderCreated", headers[RabbitMQTopology.OriginalExchangeHeader]);
        Assert.Equal(string.Empty, headers[RabbitMQTopology.OriginalRoutingKeyHeader]);
    }

    private sealed class TestCommand;

    private sealed class TestCommandHandler;

    private sealed class SecondTestCommandHandler;

    private sealed class TestNotification;

    private sealed class FirstTestNotificationHandler;

    private sealed class SecondTestNotificationHandler;

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "NOF.Infrastructure.RabbitMQ.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();

        public TestHostEnvironment()
        {
            this.ServiceName = ApplicationName;
        }
    }
}
