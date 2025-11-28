using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace NOF.Infrastructure.Tests.Extensions;

public class SendEndpointProviderExtensionsTests
{
    private class TestCommand : IAsyncCommand
    {
        public string Name { get; set; } = string.Empty;
    }

    private class CreateUserCommand : IAsyncCommand
    {
        public string Username { get; set; } = string.Empty;
    }

    [Fact]
    public async Task SendCommand_ShouldGetEndpointAndSendCommand()
    {
        // Arrange
        var mockProvider = new Mock<ISendEndpointProvider>();
        var mockEndpoint = new Mock<ISendEndpoint>();
        var command = new TestCommand { Name = "Test" };
        var destinationUri = new Uri("queue:test-queue");

        mockProvider.Setup(p => p.GetSendEndpoint(destinationUri))
            .ReturnsAsync(mockEndpoint.Object);

        // Act
        await mockProvider.Object.SendCommand(command, destinationUri, CancellationToken.None);

        // Assert
        mockProvider.Verify(p => p.GetSendEndpoint(destinationUri), Times.Once);
        mockEndpoint.Verify(e => e.Send(
            It.IsAny<object>(),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task SendCommand_ShouldCastCommandToObject()
    {
        // Arrange
        var mockProvider = new Mock<ISendEndpointProvider>();
        var mockEndpoint = new Mock<ISendEndpoint>();
        var command = new TestCommand { Name = "Test" };
        var destinationUri = new Uri("queue:test-queue");

        object? capturedMessage = null;

        mockProvider.Setup(p => p.GetSendEndpoint(destinationUri))
            .ReturnsAsync(mockEndpoint.Object);

        mockEndpoint.Setup(e => e.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((msg, ct) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        // Act
        await mockProvider.Object.SendCommand(command, destinationUri, CancellationToken.None);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.Should().BeOfType<TestCommand>();
        ((TestCommand)capturedMessage!).Name.Should().Be("Test");
    }

    [Fact]
    public async Task SendCommand_ShouldRespectCancellationToken()
    {
        // Arrange
        var mockProvider = new Mock<ISendEndpointProvider>();
        var mockEndpoint = new Mock<ISendEndpoint>();
        var command = new TestCommand { Name = "Test" };
        var destinationUri = new Uri("queue:test-queue");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockProvider.Setup(p => p.GetSendEndpoint(destinationUri))
            .ReturnsAsync(mockEndpoint.Object);

        mockEndpoint.Setup(e => e.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = async () => await mockProvider.Object.SendCommand(command, destinationUri, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendCommand_WithDefaultCancellationToken_ShouldWork()
    {
        // Arrange
        var mockProvider = new Mock<ISendEndpointProvider>();
        var mockEndpoint = new Mock<ISendEndpoint>();
        var command = new TestCommand { Name = "Test" };
        var destinationUri = new Uri("queue:test-queue");

        mockProvider.Setup(p => p.GetSendEndpoint(destinationUri))
            .ReturnsAsync(mockEndpoint.Object);

        // Act
        await mockProvider.Object.SendCommand(command, destinationUri);

        // Assert
        mockProvider.Verify(p => p.GetSendEndpoint(destinationUri), Times.Once);
        mockEndpoint.Verify(e => e.Send(
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendCommand_DifferentCommands_ShouldSendEach()
    {
        // Arrange
        var mockProvider = new Mock<ISendEndpointProvider>();
        var mockEndpoint = new Mock<ISendEndpoint>();
        var command1 = new TestCommand { Name = "Test1" };
        var command2 = new CreateUserCommand { Username = "user1" };
        var destinationUri = new Uri("queue:test-queue");

        mockProvider.Setup(p => p.GetSendEndpoint(destinationUri))
            .ReturnsAsync(mockEndpoint.Object);

        // Act
        await mockProvider.Object.SendCommand(command1, destinationUri, CancellationToken.None);
        await mockProvider.Object.SendCommand(command2, destinationUri, CancellationToken.None);

        // Assert
        mockEndpoint.Verify(e => e.Send(
            It.IsAny<object>(),
            CancellationToken.None), Times.Exactly(2));
    }

    [Fact]
    public async Task SendCommand_ShouldUseCorrectDestinationUri()
    {
        // Arrange
        var mockProvider = new Mock<ISendEndpointProvider>();
        var mockEndpoint = new Mock<ISendEndpoint>();
        var command = new TestCommand { Name = "Test" };
        var destinationUri = new Uri("queue:specific-queue");

        Uri? capturedUri = null;

        mockProvider.Setup(p => p.GetSendEndpoint(It.IsAny<Uri>()))
            .Callback<Uri>(uri => capturedUri = uri)
            .ReturnsAsync(mockEndpoint.Object);

        // Act
        await mockProvider.Object.SendCommand(command, destinationUri, CancellationToken.None);

        // Assert
        capturedUri.Should().Be(destinationUri);
    }

    [Fact]
    public async Task SendCommand_WhenEndpointThrows_ShouldPropagateException()
    {
        // Arrange
        var mockProvider = new Mock<ISendEndpointProvider>();
        var mockEndpoint = new Mock<ISendEndpoint>();
        var command = new TestCommand { Name = "Test" };
        var destinationUri = new Uri("queue:test-queue");

        mockProvider.Setup(p => p.GetSendEndpoint(destinationUri))
            .ReturnsAsync(mockEndpoint.Object);

        mockEndpoint.Setup(e => e.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Send failed"));

        // Act
        var act = async () => await mockProvider.Object.SendCommand(command, destinationUri, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Send failed");
    }

    [Fact]
    public async Task SendCommand_WhenGetEndpointFails_ShouldPropagateException()
    {
        // Arrange
        var mockProvider = new Mock<ISendEndpointProvider>();
        var command = new TestCommand { Name = "Test" };
        var destinationUri = new Uri("queue:test-queue");

        mockProvider.Setup(p => p.GetSendEndpoint(destinationUri))
            .ThrowsAsync(new Exception("Endpoint not found"));

        // Act
        var act = async () => await mockProvider.Object.SendCommand(command, destinationUri, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Endpoint not found");
    }
}
