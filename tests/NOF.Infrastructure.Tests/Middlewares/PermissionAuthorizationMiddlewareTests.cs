using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public class PermissionAuthorizationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NoEndpoint_ShouldCallNext()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        var middleware = new PermissionAuthorizationMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();

        var nextCalled = false;
        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_NoPermissionAttribute_ShouldCallNext()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        var middleware = new PermissionAuthorizationMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();

        var endpoint = new Endpoint(
            requestDelegate: ctx => Task.CompletedTask,
            metadata: new EndpointMetadataCollection(),
            displayName: "Test");

        context.SetEndpoint(endpoint);

        var nextCalled = false;
        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_RequiresPermission_UserNotAuthenticated_ShouldReturn401()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(u => u.IsAuthenticated).Returns(false);

        var middleware = new PermissionAuthorizationMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var permissionAttr = new RequirePermissionAttribute("read:users");
        var endpoint = new Endpoint(
            requestDelegate: ctx => Task.CompletedTask,
            metadata: new EndpointMetadataCollection(permissionAttr),
            displayName: "Test");

        context.SetEndpoint(endpoint);

        var nextCalled = false;
        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_RequiresPermission_UserLacksPermission_ShouldReturn403()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(u => u.IsAuthenticated).Returns(true);
        mockUserContext.Setup(u => u.HasPermission("read:users")).Returns(false);

        var middleware = new PermissionAuthorizationMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var permissionAttr = new RequirePermissionAttribute("read:users");
        var endpoint = new Endpoint(
            requestDelegate: ctx => Task.CompletedTask,
            metadata: new EndpointMetadataCollection(permissionAttr),
            displayName: "Test");

        context.SetEndpoint(endpoint);

        var nextCalled = false;
        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_RequiresPermission_UserHasPermission_ShouldCallNext()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(u => u.IsAuthenticated).Returns(true);
        mockUserContext.Setup(u => u.HasPermission("read:users")).Returns(true);

        var middleware = new PermissionAuthorizationMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();

        var permissionAttr = new RequirePermissionAttribute("read:users");
        var endpoint = new Endpoint(
            requestDelegate: ctx => Task.CompletedTask,
            metadata: new EndpointMetadataCollection(permissionAttr),
            displayName: "Test");

        context.SetEndpoint(endpoint);

        var nextCalled = false;
        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_EmptyPermission_UserAuthenticated_ShouldCallNext()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(u => u.IsAuthenticated).Returns(true);

        var middleware = new PermissionAuthorizationMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();

        var permissionAttr = new RequirePermissionAttribute("");
        var endpoint = new Endpoint(
            requestDelegate: ctx => Task.CompletedTask,
            metadata: new EndpointMetadataCollection(permissionAttr),
            displayName: "Test");

        context.SetEndpoint(endpoint);

        var nextCalled = false;
        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_NullPermission_UserAuthenticated_ShouldCallNext()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(u => u.IsAuthenticated).Returns(true);

        var middleware = new PermissionAuthorizationMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();

        var permissionAttr = new RequirePermissionAttribute(null!);
        var endpoint = new Endpoint(
            requestDelegate: ctx => Task.CompletedTask,
            metadata: new EndpointMetadataCollection(permissionAttr),
            displayName: "Test");

        context.SetEndpoint(endpoint);

        var nextCalled = false;
        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_EmptyPermission_UserNotAuthenticated_ShouldReturn401()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(u => u.IsAuthenticated).Returns(false);

        var middleware = new PermissionAuthorizationMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var permissionAttr = new RequirePermissionAttribute("");
        var endpoint = new Endpoint(
            requestDelegate: ctx => Task.CompletedTask,
            metadata: new EndpointMetadataCollection(permissionAttr),
            displayName: "Test");

        context.SetEndpoint(endpoint);

        var nextCalled = false;
        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}
