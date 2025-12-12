using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public class NotificationPublisherTests
{
    private class TestNotification : INotification
    {
        public string Message { get; set; } = string.Empty;
    }

    [Fact]
    public async Task PublishAsync_ShouldPublishNotification()
    {
        // Arrange
        var mockPublishEndpoint = new Mock<IPublishEndpoint>();
        var publisher = new NotificationPublisher(mockPublishEndpoint.Object);
        var notification = new TestNotification { Message = "Test Message" };

        // Act
        await publisher.PublishAsync(notification, CancellationToken.None);

        // Assert
        mockPublishEndpoint.Verify(
            p => p.Publish(
                It.IsAny<object>(),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldRespectCancellationToken()
    {
        // Arrange
        var mockPublishEndpoint = new Mock<IPublishEndpoint>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockPublishEndpoint.Setup(p => p.Publish(
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = new NotificationPublisher(mockPublishEndpoint.Object);
        var notification = new TestNotification { Message = "Test Message" };

        // Act
        var act = async () => await publisher.PublishAsync(notification, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PublishAsync_ShouldHandleMultipleNotifications()
    {
        // Arrange
        var mockPublishEndpoint = new Mock<IPublishEndpoint>();
        var publisher = new NotificationPublisher(mockPublishEndpoint.Object);
        var notification1 = new TestNotification { Message = "Message 1" };
        var notification2 = new TestNotification { Message = "Message 2" };

        // Act
        await publisher.PublishAsync(notification1, CancellationToken.None);
        await publisher.PublishAsync(notification2, CancellationToken.None);

        // Assert
        mockPublishEndpoint.Verify(
            p => p.Publish(
                It.IsAny<object>(),
                CancellationToken.None),
            Times.Exactly(2));
    }

    [Fact]
    public async Task PublishAsync_ShouldCastNotificationToObject()
    {
        // Arrange
        var mockPublishEndpoint = new Mock<IPublishEndpoint>();
        object? capturedMessage = null;

        mockPublishEndpoint.Setup(p => p.Publish(
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((msg, ct) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var publisher = new NotificationPublisher(mockPublishEndpoint.Object);
        var notification = new TestNotification { Message = "Test Message" };

        // Act
        await publisher.PublishAsync(notification, CancellationToken.None);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.Should().BeOfType<TestNotification>();
        ((TestNotification)capturedMessage!).Message.Should().Be("Test Message");
    }

    [Fact]
    public async Task PublishAsync_WithDefaultCancellationToken_ShouldWork()
    {
        // Arrange
        var mockPublishEndpoint = new Mock<IPublishEndpoint>();
        var publisher = new NotificationPublisher(mockPublishEndpoint.Object);
        var notification = new TestNotification { Message = "Test Message" };

        // Act
        await publisher.PublishAsync(notification, CancellationToken.None);

        // Assert
        mockPublishEndpoint.Verify(
            p => p.Publish(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
