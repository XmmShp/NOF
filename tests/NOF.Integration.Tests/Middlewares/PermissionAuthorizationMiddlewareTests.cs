using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public class PermissionAuthorizationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NoEndpoint_ShouldCallNext()
    {
        // Arrange
        var mockInvocationContext = new Mock<IInvocationContext>();
        var mockUser = new Mock<ClaimsPrincipal>();
        mockInvocationContext.Setup(x => x.User).Returns(mockUser.Object);
        
        var middleware = new PermissionAuthorizationMiddleware(mockInvocationContext.Object);
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
        var mockInvocationContext = new Mock<IInvocationContext>();
        var mockUser = new Mock<ClaimsPrincipal>();
        mockInvocationContext.Setup(x => x.User).Returns(mockUser.Object);
        
        var middleware = new PermissionAuthorizationMiddleware(mockInvocationContext.Object);
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
        var mockInvocationContext = new Mock<IInvocationContext>();
        var mockUser = new Mock<ClaimsPrincipal>();
        mockUser.Setup(u => u.Identity.IsAuthenticated).Returns(false);
        mockInvocationContext.Setup(x => x.User).Returns(mockUser.Object);

        var middleware = new PermissionAuthorizationMiddleware(mockInvocationContext.Object);
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
    public async Task InvokeAsync_EmptyPermission_UserAuthenticated_ShouldCallNext()
    {
        // Arrange
        var mockInvocationContext = new Mock<IInvocationContext>();
        var mockUser = new Mock<ClaimsPrincipal>();
        mockUser.Setup(u => u.Identity.IsAuthenticated).Returns(true);
        mockInvocationContext.Setup(x => x.User).Returns(mockUser.Object);

        var middleware = new PermissionAuthorizationMiddleware(mockInvocationContext.Object);
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
        var mockInvocationContext = new Mock<IInvocationContext>();
        var mockUser = new Mock<ClaimsPrincipal>();
        mockUser.Setup(u => u.Identity.IsAuthenticated).Returns(true);
        mockInvocationContext.Setup(x => x.User).Returns(mockUser.Object);

        var middleware = new PermissionAuthorizationMiddleware(mockInvocationContext.Object);
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
        var mockInvocationContext = new Mock<IInvocationContext>();
        var mockUser = new Mock<ClaimsPrincipal>();
        mockUser.Setup(u => u.Identity.IsAuthenticated).Returns(false);
        mockInvocationContext.Setup(x => x.User).Returns(mockUser.Object);

        var middleware = new PermissionAuthorizationMiddleware(mockInvocationContext.Object);
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
