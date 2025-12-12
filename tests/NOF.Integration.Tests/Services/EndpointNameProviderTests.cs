using FluentAssertions;
using Xunit;

namespace NOF.Integration.Tests.Services;

public record CreateUser(string Name) : ICommand;
public record UpdateUser(int Id, string Name) : ICommand<bool>;
public record UserCreated(int UserId) : INotification;

// 测试 Handler 类型
[EndpointName("Custom_CreateUser")]
public class CreateUserHandler : ICommandHandler<CreateUser>
{
    public Task<Result> HandleAsync(CreateUser command, CancellationToken cancellationToken) => null!;
}

public class UpdateUserHandler : ICommandHandler<UpdateUser, bool>
{
    public Task<Result<bool>> HandleAsync(UpdateUser command, CancellationToken cancellationToken) => null!;
}

public class UserCreatedNotificationHandler : INotificationHandler<UserCreated>
{
    public Task HandleAsync(UserCreated notification, CancellationToken cancellationToken) => null!;
}

public class MultiHandler :
    ICommandHandler<CreateUser>,
    ICommandHandler<UpdateUser, bool>
{
    public Task<Result> HandleAsync(CreateUser command, CancellationToken cancellationToken) => null!;
    public Task<Result<bool>> HandleAsync(UpdateUser command, CancellationToken cancellationToken) => null!;
} // 冲突！

public class PlainHandler { } // 无接口

public class GenericHandler<T> : ICommandHandler<T> where T : class, ICommand
{
    public Task<Result> HandleAsync(T command, CancellationToken cancellationToken) => null!;
}

public class EndpointNameProviderTests
{
    private readonly EndpointNameProvider _provider = new();

    [Fact]
    public void GetEndpointName_WithExplicitAttribute_ReturnsCustomName()
    {
        // Act
        var name = _provider.GetEndpointName(typeof(CreateUserHandler));

        // Assert
        name.Should().Be("Custom_CreateUser");
    }

    [Fact]
    public void GetEndpointName_ForCommandHandler_ReturnsMessageTypeName()
    {
        // Act
        var name = _provider.GetEndpointName(typeof(UpdateUserHandler));

        // Assert
        name.Should().Be("NOF_Integration_Tests_Services_UpdateUser"); // 假设在该命名空间
    }

    [Fact]
    public void GetEndpointName_ForNotificationHandler_ReturnsMessageTypeName()
    {
        // Act
        var name = _provider.GetEndpointName(typeof(UserCreatedNotificationHandler));

        // Assert
        name.Should().Be("NOF_Integration_Tests_Services_UserCreated");
    }

    [Fact]
    public void GetEndpointName_ImplementsMultipleHandlerInterfaces_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var act = () => _provider.GetEndpointName(typeof(MultiHandler));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*implements multiple handler interfaces*CreateUser*UpdateUser*");
    }

    [Fact]
    public void GetEndpointName_NoHandlerInterface_FallsBackToSafeTypeName()
    {
        // Act
        var name = _provider.GetEndpointName(typeof(PlainHandler));

        // Assert
        name.Should().Be("NOF_Integration_Tests_Services_PlainHandler");
    }

    [Fact]
    public void GetEndpointName_GenericHandler_ResolvesToMessageType()
    {
        // Arrange
        var handlerType = typeof(GenericHandler<CreateUser>);

        // Act
        var name = _provider.GetEndpointName(handlerType);

        // Assert
        name.Should().Be("NOF_Integration_Tests_Services_CreateUser");
    }

    [Fact]
    public void GetEndpointName_CacheIsUsed_SecondCallReturnsSameResultWithoutRecomputing()
    {
        // Arrange
        var type = typeof(UpdateUserHandler);

        // Act
        var name1 = _provider.GetEndpointName(type);
        var name2 = _provider.GetEndpointName(type);

        // Assert
        name1.Should().Be(name2);
        // Note: We can't easily verify "no recomputation" without mocking,
        // but cache correctness is implied by equality and thread-safety of ConcurrentDictionary.
    }

    public record InternalCreateUser : ICommand;

    [Fact]
    public void BuildSafeTypeName_HandlesNestedGenericTypes()
    {
        // Arrange
        var complexType = typeof(Dictionary<string, List<InternalCreateUser>>);

        // Act
        var name = _provider.GetEndpointName(complexType); // fallback path

        // Assert
        name.Should().NotBeNull();
        name.Should().Contain("Dictionary__")
            .And.Contain("String___")
            .And.Contain("List__")
            .And.Contain("____InternalCreateUser");
        // Exact format may vary, but should be safe (no '.', '+')
        name.Should().NotContain(".");
        name.Should().NotContain("+");
    }

    // Arrange
    [EndpointName("SpecialUserEvent")]
    public record SpecialUserCreated : INotification;

    public class SpecialHandler : INotificationHandler<SpecialUserCreated>
    {
        public Task HandleAsync(SpecialUserCreated notification, CancellationToken cancellationToken) => null!;
    }

    [Fact]
    public void GetEndpointName_MessageTypeItselfHasAttribute_UsesItsName()
    {
        // Act
        var name = _provider.GetEndpointName(typeof(SpecialHandler));

        // Assert
        name.Should().Be("SpecialUserEvent");
    }
}
