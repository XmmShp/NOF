using Microsoft.Extensions.Logging.Abstractions;
using NOF.Abstraction;
using NOF.Contract;
using System.Security.Claims;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public sealed class AuthorizationInboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_MethodAllowAnonymous_ShouldOverrideClassPermission_AndAllowUnauthenticated()
    {
        var userContext = new UserContext();
        var middleware = new AuthorizationInboundMiddleware(userContext, NullLogger<AuthorizationInboundMiddleware>.Instance);

        var nextCalled = false;
        await middleware.InvokeAsync(CreateContext(nameof(TestService.AllowAnonymousMethod)), _ =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_EmptyPermission_ShouldRequireAuthentication_ButNotSpecificPermission()
    {
        var userContext = new UserContext();
        var middleware = new AuthorizationInboundMiddleware(userContext, NullLogger<AuthorizationInboundMiddleware>.Instance);

        // Unauthenticated => 401
        var unauthContext = CreateContext(nameof(TestService.LoginOnlyMethod));
        await middleware.InvokeAsync(unauthContext, _ => ValueTask.CompletedTask, default);
        var unauthResult = Assert.IsType<FailResult>(unauthContext.Response);
        Assert.Equal("401", unauthResult.ErrorCode);

        // Authenticated without permissions => allowed
        userContext.User = CreateAuthenticatedUser();
        var authContext = CreateContext(nameof(TestService.LoginOnlyMethod));
        var nextCalled = false;
        await middleware.InvokeAsync(authContext, _ =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);
        Assert.True(nextCalled);
        Assert.Null(authContext.Response);
    }

    [Fact]
    public async Task InvokeAsync_MethodPermission_ShouldOverrideClassPermission()
    {
        var userContext = new UserContext
        {
            User = CreateAuthenticatedUser(ClaimTypes.Permission, "permclass")
        };
        var middleware = new AuthorizationInboundMiddleware(userContext, NullLogger<AuthorizationInboundMiddleware>.Instance);

        // Authenticated but missing method permission => 403
        var deniedContext = CreateContext(nameof(TestService.OverridePermissionMethod));
        await middleware.InvokeAsync(deniedContext, _ => ValueTask.CompletedTask, default);
        var denied = Assert.IsType<FailResult>(deniedContext.Response);
        Assert.Equal("403", denied.ErrorCode);

        // With method permission => allowed
        userContext.User = CreateAuthenticatedUser(ClaimTypes.Permission, "permmethod");
        var allowedContext = CreateContext(nameof(TestService.OverridePermissionMethod));
        var nextCalled = false;
        await middleware.InvokeAsync(allowedContext, _ =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);
        Assert.True(nextCalled);
        Assert.Null(allowedContext.Response);
    }

    private static RequestInboundContext CreateContext(string methodName)
    {
        return new RequestInboundContext
        {
            Message = new TestRequest(),
            HandlerType = typeof(TestService),
            ServiceType = typeof(TestService),
            MethodName = methodName
        };
    }

    private static ClaimsPrincipal CreateAuthenticatedUser(params string[] permissionClaims)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        for (var i = 0; i + 1 < permissionClaims.Length; i += 2)
        {
            identity.AddClaim(new Claim(permissionClaims[i], permissionClaims[i + 1]));
        }

        return new ClaimsPrincipal(identity);
    }

    [RequirePermission("permclass")]
    private sealed class TestService
    {
        [AllowAnonymous]
        public void AllowAnonymousMethod(TestRequest request) { }

        [RequirePermission]
        public void LoginOnlyMethod(TestRequest request) { }

        [RequirePermission("permmethod")]
        public void OverridePermissionMethod(TestRequest request) { }
    }

    private sealed class TestRequest;
}
