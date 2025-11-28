using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public class TestMessage
{
    public string Content { get; set; } = string.Empty;
}

public class TemporaryBusTests
{

    [Fact]
    public void Constructor_ShouldStartBus()
    {
        // Arrange
        var mockBusControl = new Mock<IBusControl>();

        // Act
        using var temporaryBus = new TemporaryBus(mockBusControl.Object);

        // Assert
        mockBusControl.Verify(
            b => b.StartAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public void Dispose_ShouldStopBus()
    {
        // Arrange
        var mockBusControl = new Mock<IBusControl>();
        var temporaryBus = new TemporaryBus(mockBusControl.Object);

        // Act
        temporaryBus.Dispose();

        // Assert
        mockBusControl.Verify(b => b.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_ShouldForwardToBusControl()
    {
        // Arrange
        var mockBusControl = new Mock<IBusControl>();
        using var temporaryBus = new TemporaryBus(mockBusControl.Object);
        var message = new TestMessage { Content = "Test" };

        // Act
        await temporaryBus.Publish(message, CancellationToken.None);

        // Assert
        mockBusControl.Verify(
            b => b.Publish(message, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task GetSendEndpoint_ShouldForwardToBusControl()
    {
        // Arrange
        var mockBusControl = new Mock<IBusControl>();
        var mockSendEndpoint = new Mock<ISendEndpoint>();
        var address = new Uri("queue:test");

        mockBusControl.Setup(b => b.GetSendEndpoint(address))
            .ReturnsAsync(mockSendEndpoint.Object);

        using var temporaryBus = new TemporaryBus(mockBusControl.Object);

        // Act
        var result = await temporaryBus.GetSendEndpoint(address);

        // Assert
        result.Should().BeSameAs(mockSendEndpoint.Object);
        mockBusControl.Verify(b => b.GetSendEndpoint(address), Times.Once);
    }

    [Fact]
    public void ConnectPublishObserver_ShouldForwardToBusControl()
    {
        // Arrange
        var mockBusControl = new Mock<IBusControl>();
        var mockObserver = new Mock<IPublishObserver>();
        var mockHandle = new Mock<ConnectHandle>();

        mockBusControl.Setup(b => b.ConnectPublishObserver(mockObserver.Object))
            .Returns(mockHandle.Object);

        using var temporaryBus = new TemporaryBus(mockBusControl.Object);

        // Act
        var result = temporaryBus.ConnectPublishObserver(mockObserver.Object);

        // Assert
        result.Should().BeSameAs(mockHandle.Object);
        mockBusControl.Verify(b => b.ConnectPublishObserver(mockObserver.Object), Times.Once);
    }

    [Fact]
    public void ConnectSendObserver_ShouldForwardToBusControl()
    {
        // Arrange
        var mockBusControl = new Mock<IBusControl>();
        var mockObserver = new Mock<ISendObserver>();
        var mockHandle = new Mock<ConnectHandle>();

        mockBusControl.Setup(b => b.ConnectSendObserver(mockObserver.Object))
            .Returns(mockHandle.Object);

        using var temporaryBus = new TemporaryBus(mockBusControl.Object);

        // Act
        var result = temporaryBus.ConnectSendObserver(mockObserver.Object);

        // Assert
        result.Should().BeSameAs(mockHandle.Object);
        mockBusControl.Verify(b => b.ConnectSendObserver(mockObserver.Object), Times.Once);
    }

    [Fact]
    public void Address_ShouldReturnBusControlAddress()
    {
        // Arrange
        var mockBusControl = new Mock<IBusControl>();
        var expectedAddress = new Uri("rabbitmq://localhost/test");
        mockBusControl.Setup(b => b.Address).Returns(expectedAddress);

        using var temporaryBus = new TemporaryBus(mockBusControl.Object);

        // Act
        var result = temporaryBus.Address;

        // Assert
        result.Should().Be(expectedAddress);
    }

    [Fact]
    public void Topology_ShouldReturnBusControlTopology()
    {
        // Arrange
        var mockBusControl = new Mock<IBusControl>();
        var mockTopology = new Mock<IBusTopology>();
        mockBusControl.Setup(b => b.Topology).Returns(mockTopology.Object);

        using var temporaryBus = new TemporaryBus(mockBusControl.Object);

        // Act
        var result = temporaryBus.Topology;

        // Assert
        result.Should().BeSameAs(mockTopology.Object);
    }

    [Fact]
    public async Task StartAsync_ShouldForwardToBusControl()
    {
        // Arrange
        var mockBusControl = new Mock<IBusControl>();
        var mockBusHandle = new Mock<BusHandle>();
        mockBusControl.Setup(b => b.StartAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBusHandle.Object);

        using var temporaryBus = new TemporaryBus(mockBusControl.Object);

        // Act
        var result = await temporaryBus.StartAsync(CancellationToken.None);

        // Assert
        result.Should().BeSameAs(mockBusHandle.Object);
    }

    [Fact]
    public async Task StopAsync_ShouldForwardToBusControl()
    {
        // Arrange
        var mockBusControl = new Mock<IBusControl>();
        using var temporaryBus = new TemporaryBus(mockBusControl.Object);

        // Act
        await temporaryBus.StopAsync(CancellationToken.None);

        // Assert
        mockBusControl.Verify(b => b.StopAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public void CheckHealth_ShouldForwardToBusControl()
    {
        // Arrange
        var mockBusControl = new Mock<IBusControl>();
        var expectedHealth = BusHealthResult.Healthy("test", new Dictionary<string, EndpointHealthResult>());
        mockBusControl.Setup(b => b.CheckHealth()).Returns(expectedHealth);

        using var temporaryBus = new TemporaryBus(mockBusControl.Object);

        // Act
        var result = temporaryBus.CheckHealth();

        // Assert
        result.Should().Be(expectedHealth);
    }

    [Fact]
    public void ConnectReceiveEndpoint_ShouldForwardToBusControl()
    {
        // Arrange
        var mockBusControl = new Mock<IBusControl>();
        var mockHandle = new Mock<HostReceiveEndpointHandle>();
        var queueName = "test-queue";

        mockBusControl.Setup(b => b.ConnectReceiveEndpoint(queueName, null))
            .Returns(mockHandle.Object);

        using var temporaryBus = new TemporaryBus(mockBusControl.Object);

        // Act
        var result = temporaryBus.ConnectReceiveEndpoint(queueName);

        // Assert
        result.Should().BeSameAs(mockHandle.Object);
    }

    [Fact]
    public async Task Publish_WithPipe_ShouldForwardToBusControl()
    {
        // Arrange
        var mockBusControl = new Mock<IBusControl>();
        var mockPipe = new Mock<IPipe<PublishContext<TestMessage>>>();
        using var temporaryBus = new TemporaryBus(mockBusControl.Object);
        var message = new TestMessage { Content = "Test" };

        // Act
        await temporaryBus.Publish(message, mockPipe.Object, CancellationToken.None);

        // Assert
        mockBusControl.Verify(
            b => b.Publish(message, mockPipe.Object, CancellationToken.None),
            Times.Once);
    }
}
