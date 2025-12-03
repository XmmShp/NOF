using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public class TestCommand : ICommand
{
    public string Name { get; set; } = string.Empty;
}

public class TestCommandWithResponse : ICommand<string>
{
    public string Name { get; set; } = string.Empty;
}

public class TestAsyncCommand : IAsyncCommand
{
    public string Name { get; set; } = string.Empty;
}

public class CommandSenderTests
{

    [Fact]
    public async Task SendAsync_ICommand_ShouldSendCommandAndReturnResult()
    {
        // Arrange
        var mockClientFactory = new Mock<IScopedClientFactory>();
        var mockSendEndpointProvider = new Mock<ISendEndpointProvider>();
        var mockRequestHandle = new Mock<RequestHandle<TestCommand>>();
        var mockResponse = new Mock<Response<Result>>();

        var expectedResult = new Result { IsSuccess = true };
        mockResponse.Setup(r => r.Message).Returns(expectedResult);
        mockRequestHandle.Setup(h => h.GetResponse<Result>()).ReturnsAsync(mockResponse.Object);

        var command = new TestCommand { Name = "Test" };
        var destinationUri = new Uri("queue:test-queue");

        mockClientFactory.Setup(f => f.CreateRequest(
            It.IsAny<Uri>(),
            It.IsAny<TestCommand>(),
            It.IsAny<CancellationToken>()))
            .Returns(mockRequestHandle.Object);

        var sender = new CommandSender(mockClientFactory.Object, mockSendEndpointProvider.Object);

        // Act
        var result = await sender.SendAsync(command, destinationUri, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        mockClientFactory.Verify(f => f.CreateRequest(
            destinationUri,
            It.IsAny<object>(),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ICommandWithResponse_ShouldSendCommandAndReturnTypedResult()
    {
        // Arrange
        var mockClientFactory = new Mock<IScopedClientFactory>();
        var mockSendEndpointProvider = new Mock<ISendEndpointProvider>();
        var mockRequestHandle = new Mock<RequestHandle<TestCommandWithResponse>>();
        var mockResponse = new Mock<Response<Result<string>>>();

        var expectedResult = new Result<string> { IsSuccess = true, Value = "Success" };
        mockResponse.Setup(r => r.Message).Returns(expectedResult);
        mockRequestHandle.Setup(h => h.GetResponse<Result<string>>()).ReturnsAsync(mockResponse.Object);

        var command = new TestCommandWithResponse { Name = "Test" };
        var destinationUri = new Uri("queue:test-queue");

        mockClientFactory.Setup(f => f.CreateRequest(
            It.IsAny<Uri>(),
            It.IsAny<TestCommandWithResponse>(),
            It.IsAny<CancellationToken>()))
            .Returns(mockRequestHandle.Object);

        var sender = new CommandSender(mockClientFactory.Object, mockSendEndpointProvider.Object);

        // Act
        var result = await sender.SendAsync(command, destinationUri, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Success");
    }

    [Fact]
    public async Task SendAsync_IAsyncCommand_ShouldSendCommandWithoutResponse()
    {
        // Arrange
        var mockClientFactory = new Mock<IScopedClientFactory>();
        var mockSendEndpointProvider = new Mock<ISendEndpointProvider>();
        var mockSendEndpoint = new Mock<ISendEndpoint>();

        var command = new TestAsyncCommand { Name = "Test" };
        var destinationUri = new Uri("queue:test-queue");

        mockSendEndpointProvider.Setup(p => p.GetSendEndpoint(destinationUri))
            .ReturnsAsync(mockSendEndpoint.Object);

        var sender = new CommandSender(mockClientFactory.Object, mockSendEndpointProvider.Object);

        // Act
        await sender.SendAsync(command, destinationUri, CancellationToken.None);

        // Assert
        mockSendEndpointProvider.Verify(p => p.GetSendEndpoint(destinationUri), Times.Once);
        mockSendEndpoint.Verify(e => e.Send(
            It.IsAny<object>(),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldRespectCancellationToken()
    {
        // Arrange
        var mockClientFactory = new Mock<IScopedClientFactory>();
        var mockSendEndpointProvider = new Mock<ISendEndpointProvider>();
        var mockRequestHandle = new Mock<RequestHandle<TestCommand>>();

        var cts = new CancellationTokenSource();
        var command = new TestCommand { Name = "Test" };
        var destinationUri = new Uri("queue:test-queue");

        mockRequestHandle.Setup(h => h.GetResponse<Result>())
            .ThrowsAsync(new OperationCanceledException());

        mockClientFactory.Setup(f => f.CreateRequest(
            It.IsAny<Uri>(),
            It.IsAny<TestCommand>(),
            It.IsAny<CancellationToken>()))
            .Returns(mockRequestHandle.Object);

        var sender = new CommandSender(mockClientFactory.Object, mockSendEndpointProvider.Object);

        // Act
        var act = async () => await sender.SendAsync(command, destinationUri, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendAsync_ICommand_WithoutDestinationUri_ShouldUseCommandQueueUri()
    {
        // Arrange
        var mockClientFactory = new Mock<IScopedClientFactory>();
        var mockSendEndpointProvider = new Mock<ISendEndpointProvider>();
        var mockRequestHandle = new Mock<RequestHandle<TestCommand>>();
        var mockResponse = new Mock<Response<Result>>();

        var expectedResult = new Result { IsSuccess = true };
        mockResponse.Setup(r => r.Message).Returns(expectedResult);
        mockRequestHandle.Setup(h => h.GetResponse<Result>()).ReturnsAsync(mockResponse.Object);

        var command = new TestCommand { Name = "Test" };

        mockClientFactory.Setup(f => f.CreateRequest(
            It.IsAny<Uri>(),
            It.IsAny<TestCommand>(),
            It.IsAny<CancellationToken>()))
            .Returns(mockRequestHandle.Object);

        var sender = new CommandSender(mockClientFactory.Object, mockSendEndpointProvider.Object);

        // Act
        var result = await sender.SendAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        mockClientFactory.Verify(f => f.CreateRequest(
            It.Is<Uri>(uri => uri.ToString().Contains("test")),
            It.IsAny<object>(),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task SendAsync_IAsyncCommand_WithoutDestinationUri_ShouldUseCommandQueueUri()
    {
        // Arrange
        var mockClientFactory = new Mock<IScopedClientFactory>();
        var mockSendEndpointProvider = new Mock<ISendEndpointProvider>();
        var mockSendEndpoint = new Mock<ISendEndpoint>();

        var command = new TestAsyncCommand { Name = "Test" };

        mockSendEndpointProvider.Setup(p => p.GetSendEndpoint(It.IsAny<Uri>()))
            .ReturnsAsync(mockSendEndpoint.Object);

        var sender = new CommandSender(mockClientFactory.Object, mockSendEndpointProvider.Object);

        // Act
        await sender.SendAsync(command, CancellationToken.None);

        // Assert
        mockSendEndpointProvider.Verify(p => p.GetSendEndpoint(It.IsAny<Uri>()), Times.Once);
    }
}
