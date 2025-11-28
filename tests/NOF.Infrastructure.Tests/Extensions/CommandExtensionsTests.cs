using FluentAssertions;
using Xunit;

namespace NOF.Infrastructure.Tests.Extensions;

public class CommandExtensionsTests
{
    private class TestCommand : ICommand
    {
        public string Name { get; set; } = string.Empty;
    }

    private class CreateUserCommand : ICommand
    {
        public string Username { get; set; } = string.Empty;
    }

    private class UpdateProductCommand : ICommand
    {
        public int ProductId { get; set; }
    }

    [Fact]
    public void GetQueueUri_ShouldReturnQueueUri()
    {
        // Arrange
        var command = new TestCommand { Name = "Test" };

        // Act
        var uri = command.GetQueueUri();

        // Assert
        uri.Should().NotBeNull();
        uri.Scheme.Should().Be("queue");
    }

    [Fact]
    public void GetQueueUri_ShouldUseKebabCaseFormatting()
    {
        // Arrange
        var command = new CreateUserCommand { Username = "testuser" };

        // Act
        var uri = command.GetQueueUri();

        // Assert
        uri.ToString().Should().Contain("create-user");
    }

    [Fact]
    public void GetQueueUri_ShouldRemoveCommandSuffix()
    {
        // Arrange
        var command = new CreateUserCommand { Username = "testuser" };

        // Act
        var uri = command.GetQueueUri();

        // Assert
        uri.ToString().Should().NotContain("-command");
        uri.ToString().Should().Be("queue:create-user");
    }

    [Fact]
    public void GetQueueUri_MultipleWords_ShouldConvertToKebabCase()
    {
        // Arrange
        var command = new UpdateProductCommand { ProductId = 1 };

        // Act
        var uri = command.GetQueueUri();

        // Assert
        uri.ToString().Should().Be("queue:update-product");
    }

    [Fact]
    public void GetQueueUri_DifferentCommands_ShouldReturnDifferentUris()
    {
        // Arrange
        var command1 = new CreateUserCommand { Username = "user1" };
        var command2 = new UpdateProductCommand { ProductId = 1 };

        // Act
        var uri1 = command1.GetQueueUri();
        var uri2 = command2.GetQueueUri();

        // Assert
        uri1.Should().NotBe(uri2);
        uri1.ToString().Should().Contain("create-user");
        uri2.ToString().Should().Contain("update-product");
    }

    [Fact]
    public void GetQueueUri_ShouldUseQueueScheme()
    {
        // Arrange
        var command = new TestCommand { Name = "Test" };

        // Act
        var uri = command.GetQueueUri();

        // Assert
        uri.Scheme.Should().Be("queue");
        uri.ToString().Should().StartWith("queue:");
    }

    [Fact]
    public void GetQueueUri_SameCommandType_ShouldReturnSameUri()
    {
        // Arrange
        var command1 = new TestCommand { Name = "Test1" };
        var command2 = new TestCommand { Name = "Test2" };

        // Act
        var uri1 = command1.GetQueueUri();
        var uri2 = command2.GetQueueUri();

        // Assert
        uri1.Should().Be(uri2);
    }

    [Fact]
    public void GetQueueUri_ShouldHandleSimpleCommandName()
    {
        // Arrange
        var command = new TestCommand { Name = "Test" };

        // Act
        var uri = command.GetQueueUri();

        // Assert
        uri.ToString().Should().Be("queue:test");
    }
}
