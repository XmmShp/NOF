using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Authentication;
using System.Security.Claims;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public class JwtUserInfoMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_ShouldSetUserContext()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        var middleware = new JwtUserInfoMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user123"),
            new(ClaimTypes.Name, "John Doe"),
            new(ClaimTypes.Role, "Admin"),
            new(ClaimTypes.Role, "User")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

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
        mockUserContext.Verify(
            u => u.SetUserAsync(
                "user123",
                "John Doe",
                It.Is<IEnumerable<string>>(roles => roles.Contains("Admin") && roles.Contains("User"))),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_ShouldNotSetUserContext()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        var middleware = new JwtUserInfoMiddleware(mockUserContext.Object);
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
        mockUserContext.Verify(
            u => u.SetUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_MissingNameIdentifier_ShouldThrowAuthenticationException()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        var middleware = new JwtUserInfoMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "John Doe")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        Task Next(HttpContext ctx) => Task.CompletedTask;

        // Act
        var act = async () => await middleware.InvokeAsync(context, Next);

        // Assert
        await act.Should().ThrowAsync<AuthenticationException>()
            .WithMessage("用户未登录");
    }

    [Fact]
    public async Task InvokeAsync_MissingName_ShouldThrowAuthenticationException()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        var middleware = new JwtUserInfoMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user123")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        Task Next(HttpContext ctx) => Task.CompletedTask;

        // Act
        var act = async () => await middleware.InvokeAsync(context, Next);

        // Assert
        await act.Should().ThrowAsync<AuthenticationException>()
            .WithMessage("用户未登录");
    }

    [Fact]
    public async Task InvokeAsync_NoRoles_ShouldSetUserContextWithEmptyRoles()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        var middleware = new JwtUserInfoMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user123"),
            new(ClaimTypes.Name, "John Doe")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        Task Next(HttpContext ctx) => Task.CompletedTask;

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        mockUserContext.Verify(
            u => u.SetUserAsync(
                "user123",
                "John Doe",
                It.Is<IEnumerable<string>>(roles => !roles.Any())),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_MultipleRoles_ShouldSetAllRoles()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        var middleware = new JwtUserInfoMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user123"),
            new(ClaimTypes.Name, "John Doe"),
            new(ClaimTypes.Role, "Admin"),
            new(ClaimTypes.Role, "User"),
            new(ClaimTypes.Role, "Manager")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        Task Next(HttpContext ctx) => Task.CompletedTask;

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        mockUserContext.Verify(
            u => u.SetUserAsync(
                "user123",
                "John Doe",
                It.Is<IEnumerable<string>>(roles =>
                    roles.Count() == 3 &&
                    roles.Contains("Admin") &&
                    roles.Contains("User") &&
                    roles.Contains("Manager"))),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNextMiddleware()
    {
        // Arrange
        var mockUserContext = new Mock<IUserContext>();
        var middleware = new JwtUserInfoMiddleware(mockUserContext.Object);
        var context = new DefaultHttpContext();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user123"),
            new(ClaimTypes.Name, "John Doe")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

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
}
