using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class CommandAuthorizationInboundMiddleware : ICommandInboundMiddleware, IAfter<CommandTenantInboundMiddleware>
{
    private readonly IUserContext _userContext;
    private readonly ILogger<CommandAuthorizationInboundMiddleware> _logger;

    public CommandAuthorizationInboundMiddleware(
        IUserContext userContext,
        ILogger<CommandAuthorizationInboundMiddleware> logger)
    {
        _userContext = userContext;
        _logger = logger;
    }

    public async ValueTask InvokeAsync(CommandInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var permissionAttr = context.Attributes.OfType<RequirePermissionAttribute>().FirstOrDefault();
        if (permissionAttr is null)
        {
            await next(cancellationToken);
            return;
        }

        var handlerName = context.HandlerName;
        var messageName = context.MessageType.FullName ?? context.MessageType.Name;

        if (!_userContext.User.IsAuthenticated)
        {
            _logger.LogWarning("Unauthenticated access to {HandlerType}/{MessageType}", handlerName, messageName);
            context.Response = Result.Fail("401", "Please login first");
            return;
        }

        if (!string.IsNullOrEmpty(permissionAttr.Permission) && !_userContext.User.HasPermission(permissionAttr.Permission))
        {
            _logger.LogWarning("Access denied to {HandlerType}/{MessageType} for user without permission {Permission}",
                handlerName, messageName, permissionAttr.Permission);
            context.Response = Result.Fail("403", "Insufficient permissions");
            return;
        }

        _logger.LogDebug("Permission check passed for {HandlerType}/{MessageType} with permission {Permission}",
            handlerName, messageName, permissionAttr.Permission);
        await next(cancellationToken);
    }
}

public sealed class NotificationAuthorizationInboundMiddleware : INotificationInboundMiddleware, IAfter<NotificationTenantInboundMiddleware>
{
    private readonly IUserContext _userContext;
    private readonly ILogger<NotificationAuthorizationInboundMiddleware> _logger;

    public NotificationAuthorizationInboundMiddleware(
        IUserContext userContext,
        ILogger<NotificationAuthorizationInboundMiddleware> logger)
    {
        _userContext = userContext;
        _logger = logger;
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var permissionAttr = context.Attributes.OfType<RequirePermissionAttribute>().FirstOrDefault();
        if (permissionAttr is null)
        {
            await next(cancellationToken);
            return;
        }

        var handlerName = context.HandlerName;
        var messageName = context.MessageType.FullName ?? context.MessageType.Name;

        if (!_userContext.User.IsAuthenticated)
        {
            _logger.LogWarning("Unauthenticated access to {HandlerType}/{MessageType}", handlerName, messageName);
            context.Response = Result.Fail("401", "Please login first");
            return;
        }

        if (!string.IsNullOrEmpty(permissionAttr.Permission) && !_userContext.User.HasPermission(permissionAttr.Permission))
        {
            _logger.LogWarning("Access denied to {HandlerType}/{MessageType} for user without permission {Permission}",
                handlerName, messageName, permissionAttr.Permission);
            context.Response = Result.Fail("403", "Insufficient permissions");
            return;
        }

        _logger.LogDebug("Permission check passed for {HandlerType}/{MessageType} with permission {Permission}",
            handlerName, messageName, permissionAttr.Permission);
        await next(cancellationToken);
    }
}

public sealed class RequestAuthorizationInboundMiddleware : IRequestInboundMiddleware, IAfter<RequestTenantInboundMiddleware>
{
    private readonly IUserContext _userContext;
    private readonly ILogger<RequestAuthorizationInboundMiddleware> _logger;

    public RequestAuthorizationInboundMiddleware(
        IUserContext userContext,
        ILogger<RequestAuthorizationInboundMiddleware> logger)
    {
        _userContext = userContext;
        _logger = logger;
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var permissionAttr = context.Attributes.OfType<RequirePermissionAttribute>().FirstOrDefault();
        if (permissionAttr is null)
        {
            await next(cancellationToken);
            return;
        }

        var handlerName = context.HandlerName;
        var requestName = $"{context.ServiceType.FullName ?? context.ServiceType.Name}.{context.MethodName}";

        if (!_userContext.User.IsAuthenticated)
        {
            _logger.LogWarning("Unauthenticated access to {HandlerType}/{MessageType}", handlerName, requestName);
            context.Response = Result.Fail("401", "Please login first");
            return;
        }

        if (!string.IsNullOrEmpty(permissionAttr.Permission) && !_userContext.User.HasPermission(permissionAttr.Permission))
        {
            _logger.LogWarning("Access denied to {HandlerType}/{MessageType} for user without permission {Permission}",
                handlerName, requestName, permissionAttr.Permission);
            context.Response = Result.Fail("403", "Insufficient permissions");
            return;
        }

        _logger.LogDebug("Permission check passed for {HandlerType}/{MessageType} with permission {Permission}",
            handlerName, requestName, permissionAttr.Permission);
        await next(cancellationToken);
    }
}
