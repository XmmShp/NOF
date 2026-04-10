using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Handler middleware that enforces permission-based authorization.
/// Checks <see cref="RequirePermissionAttribute"/> on the message/handler types and
/// short-circuits with an error response when unauthorized.
/// </summary>
public sealed class AuthorizationInboundMiddleware : IInboundMiddleware, IAfter<TenantInboundMiddleware>
{
    private readonly IUserContext _userContext;
    private readonly ILogger<AuthorizationInboundMiddleware> _logger;

    public AuthorizationInboundMiddleware(
        IUserContext userContext,
        ILogger<AuthorizationInboundMiddleware> logger)
    {
        _userContext = userContext;
        _logger = logger;
    }

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        // Check if message or handler has RequirePermissionAttribute from context.Attributes
        var permissionAttr = context.Attributes.OfType<RequirePermissionAttribute>().FirstOrDefault();

        if (permissionAttr is null)
        {
            await next(cancellationToken);
            return;
        }

        // Get handler type and message type for logging
        var handlerType = context.Metadatas.TryGetValue("HandlerType", out var handlerTypeObj) && handlerTypeObj is Type type ? type : null;
        var messageName = context.Metadatas.TryGetValue("MessageName", out var mn) ? mn as string : context.Message?.GetType().FullName;

        // Check if user is authenticated
        if (!_userContext.IsAuthenticated)
        {
            var handlerName = context.Metadatas.TryGetValue("HandlerName", out var hn) ? hn as string : handlerType?.FullName;
            _logger.LogWarning("Unauthenticated access to {HandlerType}/{MessageType}",
                handlerName, messageName);

            context.Response = Result.Fail("401", "Please login first");
            return;
        }

        // Check if user has required permission
        if (!string.IsNullOrEmpty(permissionAttr.Permission) &&
            !_userContext.HasPermission(permissionAttr.Permission))
        {
            var handlerName2 = context.Metadatas.TryGetValue("HandlerName", out var hn2) ? hn2 as string : handlerType?.FullName;
            _logger.LogWarning("Access denied to {HandlerType}/{MessageType} for user without permission {Permission}",
                handlerName2, messageName, permissionAttr.Permission);

            context.Response = Result.Fail("403", "Insufficient permissions");
            return;
        }

        var handlerName3 = context.Metadatas.TryGetValue("HandlerName", out var hn3) ? hn3 as string : handlerType?.FullName;
        _logger.LogDebug("Permission check passed for {HandlerType}/{MessageType} with permission {Permission}",
            handlerName3, messageName, permissionAttr.Permission);

        await next(cancellationToken);
    }
}
