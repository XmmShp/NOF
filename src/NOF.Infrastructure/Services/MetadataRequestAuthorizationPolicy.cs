using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Contract;

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
        _ = cancellationToken;
        var permission = GetApiPermission(context.Metadata);
        if (permission is null)
        {
            return ValueTask.FromResult<IResult?>(null);
        }

        var handlerName = context.HandlerType.DisplayName;
        var requestName = $"{context.ServiceType.DisplayName}.{context.ServiceMethodInfo.Name}";

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

    private static string? GetApiPermission(IEnumerable<object> metadata)
    {
        var attr = metadata
            .OfType<MetadataAttribute>()
            .LastOrDefault(a => string.Equals(a.Key, RequirePermissionAttribute.MetadataKey, StringComparison.OrdinalIgnoreCase));

        if (attr is null)
        {
            return null;
        }

        // null  => allow anonymous
        // ""    => require authentication only
        // "xxx" => require permission
        return attr.Value;
    }
}
