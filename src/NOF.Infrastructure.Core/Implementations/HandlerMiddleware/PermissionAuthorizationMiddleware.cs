using Microsoft.Extensions.Logging;
using System.Reflection;

namespace NOF;

/// <summary>
/// Handler permission authorization middleware
/// Handles permission checking for handlers marked with RequirePermissionAttribute
/// </summary>
public sealed class PermissionAuthorizationMiddleware : IHandlerMiddleware
{
    private readonly IInvocationContext _invocationContext;
    private readonly ILogger<PermissionAuthorizationMiddleware> _logger;

    public PermissionAuthorizationMiddleware(
        IInvocationContext invocationContext,
        ILogger<PermissionAuthorizationMiddleware> logger)
    {
        _invocationContext = invocationContext;
        _logger = logger;
    }

    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        // Check if message or handler allows anonymous access
        var allowAnonymousAttr = GetAllowAnonymousAttribute(context.Message.GetType(), context.Handler.GetType());
        if (allowAnonymousAttr is not null)
        {
            _logger.LogDebug("Handler {HandlerType} or message {MessageType} allows anonymous access", context.HandlerType, context.MessageType);
            await next(cancellationToken);
            return;
        }

        // Check if message or handler has RequirePermissionAttribute
        var permissionAttr = GetRequirePermissionAttribute(context.Message.GetType(), context.Handler.GetType());

        if (permissionAttr is not null)
        {
            // Check if user is authenticated
            if (!_invocationContext.User.IsAuthenticated)
            {
                _logger.LogWarning("Unauthorized access attempt to {HandlerType}/{MessageType} by unauthenticated user",
                    context.HandlerType, context.MessageType);

                var errorResult = Result.Fail(401, "Please login first");

                context.Response = errorResult;
                return;
            }

            // Check if user has required permission
            if (!string.IsNullOrEmpty(permissionAttr.Permission) && !_invocationContext.User.HasPermission(permissionAttr.Permission))
            {
                _logger.LogWarning("Access denied to {HandlerType}/{MessageType} for user without permission {Permission}",
                    context.HandlerType, context.MessageType, permissionAttr.Permission);

                var errorResult = Result.Fail(403, "Insufficient permissions");

                context.Response = errorResult;
                return;
            }

            _logger.LogDebug("Permission check passed for {HandlerType}/{MessageType} with permission {Permission}",
                context.HandlerType, context.MessageType, permissionAttr.Permission);
        }

        await next(cancellationToken);
    }

    private static RequirePermissionAttribute? GetRequirePermissionAttribute(Type messageType, Type handlerType)
    {
        // Check message type for attribute
        var messageTypeAttr = messageType.GetCustomAttribute<RequirePermissionAttribute>(false);

        if (messageTypeAttr is not null)
        {
            return messageTypeAttr;
        }

        // Check handler type for attribute
        var handlerTypeAttr = handlerType.GetCustomAttribute<RequirePermissionAttribute>(false);

        return handlerTypeAttr;
    }

    private static AllowAnonymousAttribute? GetAllowAnonymousAttribute(Type messageType, Type handlerType)
    {
        // Check message type for attribute
        var messageTypeAttr = messageType.GetCustomAttribute<AllowAnonymousAttribute>(false);

        if (messageTypeAttr is not null)
        {
            return messageTypeAttr;
        }

        // Check handler type for attribute
        var handlerTypeAttr = handlerType.GetCustomAttribute<AllowAnonymousAttribute>(false);

        return handlerTypeAttr;
    }
}
