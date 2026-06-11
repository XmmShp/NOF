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
        var request = new TestRequest();

        var nextCalled = false;
        await middleware.InvokeAsync(CreateContext(nameof(TestService.AllowAnonymousMethod)), request, (_, _, _) =>
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
        await middleware.InvokeAsync(unauthContext, new TestRequest(), (_, _, _) => ValueTask.CompletedTask, default);
        var unauthResult = Assert.IsAssignableFrom<IResult>(unauthContext.Response!.Body);
        Assert.False(unauthResult.IsSuccess);
        Assert.Equal("401", unauthResult.ErrorCode);
        Assert.Equal("Please login first", unauthResult.Message);

        // Authenticated without permissions => allowed
        userContext.Logout();
        userContext.User.AddIdentity(CreateAuthenticatedIdentity());
        var authContext = CreateContext(nameof(TestService.LoginOnlyMethod));
        var nextCalled = false;
        await middleware.InvokeAsync(authContext, new TestRequest(), (_, _, _) =>
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
        var userContext = new UserContext();
        userContext.User.AddIdentity(CreateAuthenticatedIdentity(ClaimTypes.Permission, "permclass"));
        var middleware = CreateMiddleware(userContext);

        // Authenticated but missing method permission => 403
        var deniedContext = CreateContext(nameof(TestService.OverridePermissionMethod));
        await middleware.InvokeAsync(deniedContext, new TestRequest(), (_, _, _) => ValueTask.CompletedTask, default);
        var deniedResult = Assert.IsAssignableFrom<IResult>(deniedContext.Response!.Body);
        Assert.False(deniedResult.IsSuccess);
        Assert.Equal("403", deniedResult.ErrorCode);
        Assert.Equal("Insufficient permissions", deniedResult.Message);

        // With method permission => allowed
        userContext.Logout();
        userContext.User.AddIdentity(CreateAuthenticatedIdentity(ClaimTypes.Permission, "permmethod"));
        var allowedContext = CreateContext(nameof(TestService.OverridePermissionMethod));
        var nextCalled = false;
        await middleware.InvokeAsync(allowedContext, new TestRequest(), (_, _, _) =>
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

        await middleware.InvokeAsync(context, new TestRequest(), (_, _, _) =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);

        var denied = Assert.IsAssignableFrom<IResult>(context.Response!.Body);
        Assert.False(denied.IsSuccess);
        Assert.Equal("499", denied.ErrorCode);
        Assert.Equal("custom policy denied", denied.Message);
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

        await middleware.InvokeAsync(context, new TestRequest(), (_, _, _) =>
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

        await middleware.InvokeAsync(context, new TestRequest(), (_, _, _) => ValueTask.CompletedTask, default);

        var denied = Assert.IsAssignableFrom<IResult>(context.Response!.Body);
        Assert.False(denied.IsSuccess);
        Assert.Equal("498", denied.ErrorCode);
        Assert.Equal("custom policy denied", denied.Message);
    }

    [Fact]
    public async Task InvokeAsync_NonResultResponse_ShouldShortCircuitAsTransportFailure()
    {
        var userContext = new UserContext();
        var middleware = CreateMiddleware(userContext);
        var context = CreateContext(nameof(TestService.LoginOnlyMethod), typeof(Empty));

        await middleware.InvokeAsync(context, new TestRequest(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.False(context.Response!.IsSuccess);
        Assert.Null(context.Response.Body);
        Assert.True(HttpTransportMetadata.TryGetStatusCode(context.Response.Metadatas, out var statusCode));
        Assert.Equal(401, statusCode);
    }

    private static RequestInboundContext CreateContext(string methodName, Type? responseType = null)
    {
        var serviceMethod = typeof(TestService).GetMethod(methodName)!;
        return new RequestInboundContext
        {
            ServiceType = typeof(TestService),
            ServiceMethodInfo = serviceMethod,
            HandlerType = typeof(TestService),
            HandlerMethodInfo = serviceMethod,
            RequestType = typeof(TestRequest),
            ResponseType = responseType ?? typeof(Result),
            Metadata =
            [
                .. typeof(TestService).GetCustomAttributes(inherit: true),
                .. serviceMethod.GetCustomAttributes(inherit: true)
            ]
        };
    }

    private static AuthorizationInboundMiddleware CreateMiddleware(IUserContext userContext)
    {
        var policy = new MetadataRequestAuthorizationPolicy(
            userContext,
            NullLogger<MetadataRequestAuthorizationPolicy>.Instance);
        return new AuthorizationInboundMiddleware([policy]);
    }

    private static ClaimsIdentity CreateAuthenticatedIdentity(params string[] permissionClaims)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        for (var i = 0; i + 1 < permissionClaims.Length; i += 2)
        {
            identity.AddClaim(new Claim(permissionClaims[i], permissionClaims[i + 1]));
        }

        return identity;
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
        {
            _ = context;
            _ = cancellationToken;
            return ValueTask.FromResult<IResult?>(null);
        }
    }

    private sealed class DenyAllAuthorizationPolicy(string errorCode) : IRequestAuthorizationPolicy
    {
        public ValueTask<IResult?> AuthorizeAsync(RequestInboundContext context, CancellationToken cancellationToken)
        {
            _ = context;
            _ = cancellationToken;
            return ValueTask.FromResult<IResult?>(Result.Fail(errorCode, "custom policy denied"));
        }
    }
}
