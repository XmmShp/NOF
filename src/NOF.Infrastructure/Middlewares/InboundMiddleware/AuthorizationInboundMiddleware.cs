using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Hosting;
using NOF.Contract;

namespace NOF.Infrastructure;

public sealed class AuthorizationInboundMiddleware : AllMessagesInboundMiddleware, IAfter<TenantInboundMiddleware>
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

    protected override async ValueTask InvokeAsyncCore(MessageInboundContext context, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken)
    {
        var permissionAttr = context.Attributes.OfType<RequirePermissionAttribute>().FirstOrDefault();
        if (permissionAttr is null)
        {
            await next(cancellationToken);
            return;
        }

        var handlerName = context.HandlerName;
        var messageName = context.MessageName;

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
