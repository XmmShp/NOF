using FluentAssertions;
using MassTransit;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public class EndpointNameFormatterTests
{
    private class TestConsumer : IConsumer
    {
    }

    [ConsumerQueue("another-custom-queue")]
    private class CustomQueueConsumer : IConsumer
    {
    }

    private class TestCommand : ICommand
    {
    }

    private class CreateUserCommand : ICommand
    {
    }

    private class UpdateProductCommand : ICommand
    {
    }

    [Fact]
    public void Consumer_WithoutAttribute_ShouldUseDefaultKebabCaseFormat()
    {
        // Arrange
        var formatter = EndpointNameFormatter.Instance;

        // Act
        var result = formatter.Consumer<TestConsumer>();

        // Assert
        result.Should().Be("test");
    }

    [Fact]
    public void Consumer_WithConsumerQueueAttribute_ShouldUseCustomQueueName()
    {
        // Arrange
        var formatter = EndpointNameFormatter.Instance;

        // Act
        var result = formatter.Consumer<CustomQueueConsumer>();

        // Assert
        result.Should().Be("another-custom-queue");
    }

    [Fact]
    public void GetMessageName_WithCommandSuffix_ShouldRemoveCommandSuffix()
    {
        // Arrange
        var formatter = EndpointNameFormatter.Instance;
        var command = new CreateUserCommand();

        // Act
        var result = formatter.GetMessageName(command);

        // Assert
        result.Should().Be("create-user");
    }

    [Fact]
    public void GetMessageName_WithoutCommandSuffix_ShouldReturnKebabCase()
    {
        // Arrange
        var formatter = EndpointNameFormatter.Instance;
        var command = new TestCommand();

        // Act
        var result = formatter.GetMessageName(command);

        // Assert
        result.Should().Be("test");
    }

    [Fact]
    public void GetMessageName_MultipleWords_ShouldConvertToKebabCase()
    {
        // Arrange
        var formatter = EndpointNameFormatter.Instance;
        var command = new UpdateProductCommand();

        // Act
        var result = formatter.GetMessageName(command);

        // Assert
        result.Should().Be("update-product");
    }

    [Fact]
    public void Instance_ShouldReturnSingletonInstance()
    {
        // Arrange & Act
        var instance1 = EndpointNameFormatter.Instance;
        var instance2 = EndpointNameFormatter.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void GetMessageName_ShouldHandleComplexCommandNames()
    {
        // Arrange
        var formatter = EndpointNameFormatter.Instance;

        // Create a test type dynamically to simulate complex naming
        var testCommand = new CreateUserCommand();

        // Act
        var result = formatter.GetMessageName(testCommand);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().NotContain("-command");
    }

    [Fact]
    public void GetMessageName_WithObject_ShouldUseObjectType()
    {
        // Arrange
        var formatter = EndpointNameFormatter.Instance;
        object command = new CreateUserCommand();

        // Act
        var result = formatter.GetMessageName(command);

        // Assert
        result.Should().Be("create-user");
    }
}
