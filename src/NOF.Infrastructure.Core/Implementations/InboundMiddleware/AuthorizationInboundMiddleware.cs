using Microsoft.Extensions.Logging;
using NOF.Application;
using NOF.Contract;
using System.Reflection;

namespace NOF.Infrastructure.Core;

/// <summary>Permission authorization step — checks [RequirePermission] / [AllowAnonymous].</summary>
public class AuthorizationInboundMiddlewareStep : IInboundMiddlewareStep<AuthorizationInboundMiddleware>, IAfter<TenantInboundMiddlewareStep>;

/// <summary>
/// Handler middleware that enforces permission-based authorization.
/// Checks <see cref="AllowAnonymousAttribute"/> and <see cref="RequirePermissionAttribute"/>
/// on the message/handler types and short-circuits with an error response when unauthorized.
/// </summary>
public sealed class AuthorizationInboundMiddleware : IInboundMiddleware
{
    private readonly IInvocationContext _invocationContext;
    private readonly ILogger<AuthorizationInboundMiddleware> _logger;

    public AuthorizationInboundMiddleware(
        IInvocationContext invocationContext,
        ILogger<AuthorizationInboundMiddleware> logger)
    {
        _invocationContext = invocationContext;
        _logger = logger;
    }

    public async ValueTask InvokeAsync(InboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var messageType = context.Message.GetType();
        var handlerType = context.Handler.GetType();

        // Check if message or handler allows anonymous access
        var allowAnonymousAttr = GetAttribute<AllowAnonymousAttribute>(messageType, handlerType);
        if (allowAnonymousAttr is not null)
        {
            _logger.LogDebug("Handler {HandlerType} or message {MessageType} allows anonymous access",
                context.HandlerType, context.MessageType);
            await next(cancellationToken);
            return;
        }

        // Check if message or handler has RequirePermissionAttribute
        var permissionAttr = GetAttribute<RequirePermissionAttribute>(messageType, handlerType);

        if (permissionAttr is null)
        {
            await next(cancellationToken);
            return;
        }

        // Check if user is authenticated
        if (!_invocationContext.User.IsAuthenticated)
        {
            _logger.LogWarning("Unauthorized access attempt to {HandlerType}/{MessageType} by unauthenticated user",
                context.HandlerType, context.MessageType);

            context.Response = Result.Fail(401, "Please login first");
            return;
        }

        // Check if user has required permission
        if (!string.IsNullOrEmpty(permissionAttr.Permission) &&
            !_invocationContext.User.HasPermission(permissionAttr.Permission))
        {
            _logger.LogWarning("Access denied to {HandlerType}/{MessageType} for user without permission {Permission}",
                context.HandlerType, context.MessageType, permissionAttr.Permission);

            context.Response = Result.Fail(403, "Insufficient permissions");
            return;
        }

        _logger.LogDebug("Permission check passed for {HandlerType}/{MessageType} with permission {Permission}",
            context.HandlerType, context.MessageType, permissionAttr.Permission);

        await next(cancellationToken);
    }

    private static T? GetAttribute<T>(Type messageType, Type handlerType) where T : Attribute
    {
        return messageType.GetCustomAttribute<T>(false)
               ?? handlerType.GetCustomAttribute<T>(false);
    }
}
