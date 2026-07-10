using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        await middleware.InvokeAsync(CreateRequestContext(nameof(TestService.AllowAnonymousMethod)), request, (_, _, _) =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_EmptyPermission_ShouldRequireAuthentication_AndSetAuthenticateHeader()
    {
        var userContext = new UserContext();
        var middleware = CreateMiddleware(userContext);

        var context = CreateRequestContext(nameof(TestService.LoginOnlyMethod));
        await middleware.InvokeAsync(context, new TestRequest(), (_, _, _) => ValueTask.CompletedTask, default);

        var response = Assert.IsAssignableFrom<IResult>(context.Response);
        Assert.False(response.IsSuccess);
        Assert.Equal("401", response.ErrorCode);
        Assert.Equal("Please login first", response.Message);
        Assert.Equal("401", context.ResponseHeaders[NOFInfrastructureConstants.Transport.Headers.HttpStatusCode]);
        Assert.Equal(
            "Bearer error=\"invalid_token\", authorization_server=\"https://auth.local/oauth2\"",
            context.ResponseHeaders["WWW-Authenticate"]);
    }

    [Fact]
    public async Task InvokeAsync_RequestInputMethodPermission_ShouldOverrideInputTypePermission()
    {
        var userContext = new UserContext();
        userContext.User.AddIdentity(CreateAuthenticatedIdentity(ClaimTypes.Permission, "input-type"));
        var middleware = CreateMiddleware(userContext);

        var deniedContext = CreateRequestContext(nameof(TestService.OverridePermissionMethod), handlerMethodName: nameof(TestHandler.AllowAnonymousHandler));
        await middleware.InvokeAsync(deniedContext, new TestRequest(), (_, _, _) => ValueTask.CompletedTask, default);

        var denied = Assert.IsAssignableFrom<IResult>(deniedContext.Response);
        Assert.Equal("403", denied.ErrorCode);
        Assert.Equal("403", deniedContext.ResponseHeaders[NOFInfrastructureConstants.Transport.Headers.HttpStatusCode]);
        Assert.Equal(
            "Bearer error=\"insufficient_scope\", authorization_server=\"https://auth.local/oauth2\", scope=\"input-method\"",
            deniedContext.ResponseHeaders["WWW-Authenticate"]);

        userContext.Logout();
        userContext.User.AddIdentity(CreateAuthenticatedIdentity(ClaimTypes.Permission, "input-method"));
        var allowedContext = CreateRequestContext(nameof(TestService.OverridePermissionMethod), handlerMethodName: nameof(TestHandler.AllowAnonymousHandler));
        var nextCalled = false;
        await middleware.InvokeAsync(allowedContext, new TestRequest(), (_, _, _) =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Request_ShouldRequireInputAndExecutionPermissions()
    {
        var userContext = new UserContext();
        userContext.User.AddIdentity(CreateAuthenticatedIdentity(ClaimTypes.Permission, "input-method"));
        var middleware = CreateMiddleware(userContext);

        var deniedContext = CreateRequestContext(nameof(TestService.OverridePermissionMethod), handlerMethodName: nameof(TestHandler.ExecutePermissionHandler));
        await middleware.InvokeAsync(deniedContext, new TestRequest(), (_, _, _) => ValueTask.CompletedTask, default);

        var denied = Assert.IsAssignableFrom<IResult>(deniedContext.Response);
        Assert.Equal("403", denied.ErrorCode);
        Assert.Equal(
            "Bearer error=\"insufficient_scope\", authorization_server=\"https://auth.local/oauth2\", scope=\"execute-method\"",
            deniedContext.ResponseHeaders["WWW-Authenticate"]);

        userContext.Logout();
        userContext.User.AddIdentity(CreateAuthenticatedIdentity(
            ClaimTypes.Permission, "input-method",
            ClaimTypes.Permission, "execute-method"));
        var allowedContext = CreateRequestContext(nameof(TestService.OverridePermissionMethod), handlerMethodName: nameof(TestHandler.ExecutePermissionHandler));
        var nextCalled = false;
        await middleware.InvokeAsync(allowedContext, new TestRequest(), (_, _, _) =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_DefaultHandler_ShouldNotTreatScopeAsPermission()
    {
        var userContext = new UserContext();
        userContext.User.AddIdentity(CreateAuthenticatedIdentity("scope", "input-method"));
        var middleware = CreateMiddleware(userContext);

        var context = CreateRequestContext(nameof(TestService.OverridePermissionMethod), handlerMethodName: nameof(TestHandler.AllowAnonymousHandler));
        await middleware.InvokeAsync(
            context,
            new TestRequest(),
            (_, _, _) => ValueTask.CompletedTask,
            default);

        var denied = Assert.IsAssignableFrom<IResult>(context.Response);
        Assert.Equal("403", denied.ErrorCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthenticatedUserHasTenant_ShouldOverrideCurrentTenant()
    {
        var userContext = new UserContext();
        userContext.User.AddIdentity(CreateAuthenticatedIdentity(
            ClaimTypes.Permission, "input-method",
            ClaimTypes.TenantId, "tenantb"));
        var currentTenant = new CurrentTenant();
        using var tenantScope = currentTenant.PushTenant("tenanta");
        var middleware = CreateMiddleware(userContext, currentTenant);
        var context = CreateRequestContext(nameof(TestService.OverridePermissionMethod), handlerMethodName: nameof(TestHandler.AllowAnonymousHandler));

        var tenantDuringNext = string.Empty;
        await middleware.InvokeAsync(context, new TestRequest(), (_, _, _) =>
        {
            tenantDuringNext = currentTenant.TenantId;
            return ValueTask.CompletedTask;
        }, default);

        Assert.Equal("tenantb", tenantDuringNext);
        Assert.Equal("tenanta", currentTenant.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_Command_ShouldRequireMessageAndHandlerPermissions()
    {
        var userContext = new UserContext();
        userContext.User.AddIdentity(CreateAuthenticatedIdentity(ClaimTypes.Permission, "message"));
        var middleware = CreateMiddleware(userContext);
        var context = CreateCommandContext(typeof(ProtectedCommand), typeof(CommandHandlerWithPermission), nameof(CommandHandlerWithPermission.Handle));

        var nextCalled = false;
        await middleware.InvokeAsync(context, new ProtectedCommand(), (_, _, _) =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);
        Assert.False(nextCalled);

        userContext.Logout();
        userContext.User.AddIdentity(CreateAuthenticatedIdentity(
            ClaimTypes.Permission, "message",
            ClaimTypes.Permission, "handler"));
        await middleware.InvokeAsync(context, new ProtectedCommand(), (_, _, _) =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Notification_ShouldRequireMessageAndHandlerPermissions()
    {
        var userContext = new UserContext();
        userContext.User.AddIdentity(CreateAuthenticatedIdentity(
            ClaimTypes.Permission, "message",
            ClaimTypes.Permission, "handler"));
        var middleware = CreateMiddleware(userContext);
        var context = CreateNotificationContext(typeof(ProtectedNotification), typeof(NotificationHandlerWithPermission), nameof(NotificationHandlerWithPermission.Handle));

        var nextCalled = false;
        await middleware.InvokeAsync(context, new ProtectedNotification(), (_, _, _) =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_CustomAuthorizationHandler_ShouldReceiveFullContext()
    {
        var userContext = new UserContext();
        userContext.User.AddIdentity(CreateAuthenticatedIdentity());
        var handler = new TestInboundAuthorizationHandler();
        var middleware = CreateMiddleware(userContext, authorizationHandler: handler);
        var request = new TestRequest();

        var nextCalled = false;
        await middleware.InvokeAsync(
            CreateRequestContext(nameof(TestService.OverridePermissionMethod), handlerMethodName: nameof(TestHandler.ExecutePermissionHandler)),
            request,
            (_, forwardedRequest, _) =>
            {
                Assert.Same(request, forwardedRequest);
                nextCalled = true;
                return ValueTask.CompletedTask;
            },
            default);

        Assert.True(nextCalled);
        Assert.NotNull(handler.LastContext);
        Assert.Equal(InboundAuthorizationKind.Request, handler.LastContext!.Kind);
        Assert.Same(request, handler.LastContext.Input);
        Assert.Equal(typeof(TestService), handler.LastContext.ServiceType);
        Assert.Equal(nameof(TestService.OverridePermissionMethod), handler.LastContext.ServiceMethodInfo?.Name);
        Assert.Equal(typeof(TestHandler), handler.LastContext.HandlerType);
        Assert.Equal(nameof(TestHandler.ExecutePermissionHandler), handler.LastContext.HandlerMethodInfo.Name);
        Assert.Null(handler.LastContext.MessageType);
    }

    private static RequestInboundContext CreateRequestContext(string serviceMethodName, Type? responseType = null, string? handlerMethodName = null)
    {
        var serviceMethod = typeof(TestService).GetMethod(serviceMethodName)!;
        var handlerMethod = typeof(TestHandler).GetMethod(handlerMethodName ?? nameof(TestHandler.AllowAnonymousHandler))!;
        return new RequestInboundContext
        {
            ServiceType = typeof(TestService),
            ServiceMethodInfo = serviceMethod,
            HandlerType = typeof(TestHandler),
            HandlerMethodInfo = handlerMethod,
            RequestType = typeof(TestRequest),
            ResponseType = responseType ?? typeof(Result)
        };
    }

    private static CommandInboundContext CreateCommandContext(Type messageType, Type handlerType, string methodName)
    {
        return new CommandInboundContext
        {
            MethodInfo = handlerType.GetMethod(methodName)!,
            HandlerType = handlerType,
            MessageType = messageType
        };
    }

    private static NotificationInboundContext CreateNotificationContext(Type messageType, Type handlerType, string methodName)
    {
        return new NotificationInboundContext
        {
            MethodInfo = handlerType.GetMethod(methodName)!,
            HandlerType = handlerType,
            MessageType = messageType
        };
    }

    private static AuthorizationInboundMiddleware CreateMiddleware(
        IUserContext userContext,
        IMutableCurrentTenant? currentTenant = null,
        IInboundAuthorizationHandler? authorizationHandler = null)
        => new(
            userContext,
            authorizationHandler ?? new DefaultInboundAuthorizationHandler(NullLogger<DefaultInboundAuthorizationHandler>.Instance),
            currentTenant ?? new CurrentTenant(),
            Options.Create(new AuthenticationResourceServerOptions
            {
                AuthorizationServerIssuer = "https://auth.local/oauth2"
            }));

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

    [RequirePermission("input-type")]
    private sealed class TestService
    {
        [AllowAnonymous]
        public void AllowAnonymousMethod(TestRequest request) { }

        [RequirePermission]
        public void LoginOnlyMethod(TestRequest request) { }

        [RequirePermission("input-method")]
        public void OverridePermissionMethod(TestRequest request) { }
    }

    private sealed class TestHandler
    {
        [AllowAnonymous]
        public void AllowAnonymousHandler(TestRequest request) { }

        [RequirePermission("execute-method")]
        public void ExecutePermissionHandler(TestRequest request) { }
    }

    private sealed class TestRequest;

    [RequirePermission("message")]
    private sealed class ProtectedCommand;

    [RequirePermission("message")]
    private sealed class ProtectedNotification;

    private sealed class CommandHandlerWithPermission
    {
        [RequirePermission("handler")]
        public void Handle(ProtectedCommand command) { }
    }

    private sealed class NotificationHandlerWithPermission
    {
        [RequirePermission("handler")]
        public void Handle(ProtectedNotification notification) { }
    }

    private sealed class TestInboundAuthorizationHandler : IInboundAuthorizationHandler
    {
        public InboundAuthorizationContext? LastContext { get; private set; }

        public ValueTask<InboundAuthorizationResult> AuthorizeAsync(
            InboundAuthorizationContext context,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            LastContext = context;
            return ValueTask.FromResult<InboundAuthorizationResult>(InboundAuthorizationResult.Success);
        }
    }
}
