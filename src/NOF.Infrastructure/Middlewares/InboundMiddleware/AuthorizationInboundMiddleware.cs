using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;
using System.Reflection;

namespace NOF.Infrastructure;

public sealed class AuthorizationInboundMiddleware :
    IRequestInboundMiddleware,
    IAfter<TenantInboundMiddleware>
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

    public async ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var permissionAttr = GetPermissionAttribute(context.ServiceType, context.MethodName, context.Message.GetType());
        if (permissionAttr is null)
        {
            await next(cancellationToken);
            return;
        }

        var handlerName = context.HandlerType.DisplayName;
        var requestName = $"{context.ServiceType.DisplayName}.{context.MethodName}";

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

    private static RequirePermissionAttribute? GetPermissionAttribute(Type serviceType, string methodName, Type requestType)
    {
        // Prefer request type attribute (works for both IRpcService method request DTOs and messages).
        var typeAttribute = requestType.GetCustomAttributes(true).OfType<RequirePermissionAttribute>().FirstOrDefault();
        if (typeAttribute is not null)
        {
            return typeAttribute;
        }

        // Then fall back to service method attribute.
        var method = serviceType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        return method?.GetCustomAttributes(true).OfType<RequirePermissionAttribute>().FirstOrDefault();
    }
}
