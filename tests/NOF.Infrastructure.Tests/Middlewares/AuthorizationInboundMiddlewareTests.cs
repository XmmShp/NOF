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
        var middleware = CreateMiddleware(userContext);

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
        var middleware = CreateMiddleware(userContext);

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
        var middleware = CreateMiddleware(userContext);

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

    [Fact]
    public async Task InvokeAsync_CustomPolicy_ShouldShortCircuitWithPolicyResponse()
    {
        var middleware = new AuthorizationInboundMiddleware([new DenyAllAuthorizationPolicy("499")]);
        var context = CreateContext(nameof(TestService.AllowAnonymousMethod));
        var nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);

        var denied = Assert.IsType<FailResult>(context.Response);
        Assert.Equal("499", denied.ErrorCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_MultiplePolicies_ShouldAllowWhenAnyPolicyAllows()
    {
        var middleware = new AuthorizationInboundMiddleware([
            new DenyAllAuthorizationPolicy("498"),
            new AllowAllAuthorizationPolicy()
        ]);
        var context = CreateContext(nameof(TestService.LoginOnlyMethod));
        var nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);

        Assert.True(nextCalled);
        Assert.Null(context.Response);
    }

    [Fact]
    public async Task InvokeAsync_MultiplePolicies_ShouldUseFirstFailureWhenAllPoliciesDeny()
    {
        var middleware = new AuthorizationInboundMiddleware([
            new DenyAllAuthorizationPolicy("498"),
            new DenyAllAuthorizationPolicy("499")
        ]);
        var context = CreateContext(nameof(TestService.LoginOnlyMethod));

        await middleware.InvokeAsync(context, _ => ValueTask.CompletedTask, default);

        var denied = Assert.IsType<FailResult>(context.Response);
        Assert.Equal("498", denied.ErrorCode);
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

    private static AuthorizationInboundMiddleware CreateMiddleware(IUserContext userContext)
    {
        var policy = new MetadataRequestAuthorizationPolicy(
            userContext,
            NullLogger<MetadataRequestAuthorizationPolicy>.Instance);
        return new AuthorizationInboundMiddleware([policy]);
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

    private sealed class AllowAllAuthorizationPolicy : IRequestAuthorizationPolicy
    {
        public ValueTask<IResult?> AuthorizeAsync(RequestInboundContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult<IResult?>(null);
    }

    private sealed class DenyAllAuthorizationPolicy(string errorCode) : IRequestAuthorizationPolicy
    {
        public ValueTask<IResult?> AuthorizeAsync(RequestInboundContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult<IResult?>(Result.Fail(errorCode, "custom policy denied"));
    }
}
