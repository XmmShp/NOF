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
}
