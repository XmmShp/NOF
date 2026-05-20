using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Annotation;
using NOF.Contract;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Infrastructure;

/// <summary>
/// Default request authorization policy based on <see cref="RequirePermissionAttribute" /> metadata.
/// </summary>
public sealed class MetadataRequestAuthorizationPolicy : IRequestAuthorizationPolicy
{
    private readonly IUserContext _userContext;
    private readonly ILogger<MetadataRequestAuthorizationPolicy> _logger;

    public MetadataRequestAuthorizationPolicy(
        IUserContext userContext,
        ILogger<MetadataRequestAuthorizationPolicy> logger)
    {
        _userContext = userContext;
        _logger = logger;
    }

    public ValueTask<IResult?> AuthorizeAsync(RequestInboundContext context, CancellationToken cancellationToken)
    {
        var permission = GetApiPermission(context.ServiceType, context.MethodName);
        if (permission is null)
        {
            return ValueTask.FromResult<IResult?>(null);
        }

        var handlerName = context.HandlerType.DisplayName;
        var requestName = $"{context.ServiceType.DisplayName}.{context.MethodName}";

        if (!_userContext.User.IsAuthenticated)
        {
            _logger.LogWarning("Unauthenticated access to {HandlerType}/{MessageType}", handlerName, requestName);
            return ValueTask.FromResult<IResult?>(Result.Fail("401", "Please login first"));
        }

        if (permission.Length > 0 && !_userContext.User.HasPermission(permission))
        {
            _logger.LogWarning("Access denied to {HandlerType}/{MessageType} for user without permission {Permission}",
                handlerName, requestName, permission);
            return ValueTask.FromResult<IResult?>(Result.Fail("403", "Insufficient permissions"));
        }

        _logger.LogDebug("Authorization passed for {HandlerType}/{MessageType} with permission {Permission}",
            handlerName, requestName, permission);
        return ValueTask.FromResult<IResult?>(null);
    }

    private static string? GetApiPermission([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type serviceType, string methodName)
    {
        var method = serviceType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        if (TryGetApiPermissionValue(method, out var methodPermission))
        {
            return methodPermission;
        }

        if (TryGetApiPermissionValue(serviceType, out var servicePermission))
        {
            return servicePermission;
        }

        return null;
    }

    private static bool TryGetApiPermissionValue(MemberInfo? member, out string? permission)
    {
        permission = null;
        if (member is null)
        {
            return false;
        }

        var attr = member
            .GetCustomAttributes(true)
            .OfType<MetadataAttribute>()
            .LastOrDefault(a => string.Equals(a.Key, RequirePermissionAttribute.MetadataKey, StringComparison.OrdinalIgnoreCase));

        if (attr is null)
        {
            return false;
        }

        // null  => allow anonymous
        // ""    => require authentication only
        // "xxx" => require permission
        permission = attr.Value;
        return true;
    }

    private static bool TryGetApiPermissionValue(Type type, out string? permission)
    {
        permission = null;

        var attr = type
            .GetCustomAttributes(true)
            .OfType<MetadataAttribute>()
            .LastOrDefault(a => string.Equals(a.Key, RequirePermissionAttribute.MetadataKey, StringComparison.OrdinalIgnoreCase));

        if (attr is null)
        {
            return false;
        }

        permission = attr.Value;
        return true;
    }
}
