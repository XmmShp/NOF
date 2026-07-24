using Microsoft.Extensions.Logging;
using NOF.Contract;
using System.Reflection;
using System.Security.Claims;

namespace NOF.Infrastructure;

public sealed class DefaultInboundAuthorizationHandler(
    ILogger<DefaultInboundAuthorizationHandler> logger) : IInboundAuthorizationHandler
{
    public ValueTask<InboundAuthorizationResult> AuthorizeAsync(
        InboundAuthorizationContext context,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var inputRequirement = ResolveInputRequirement(context);
        var inputFailure = AuthorizeRequirement(
            context,
            inputRequirement,
            ResolveOperationName(context, input: true));
        if (inputFailure is not null)
        {
            return ValueTask.FromResult<InboundAuthorizationResult>(inputFailure);
        }

        var executionRequirement = ResolveExecutionRequirement(context);
        var executionFailure = AuthorizeRequirement(
            context,
            executionRequirement,
            ResolveOperationName(context, input: false));
        if (executionFailure is not null)
        {
            return ValueTask.FromResult<InboundAuthorizationResult>(executionFailure);
        }

        logger.LogDebug(
            "Authorization passed for {HandlerType}/{OperationName}",
            context.HandlerType.DisplayName,
            ResolveOperationName(context, input: false));
        return ValueTask.FromResult<InboundAuthorizationResult>(InboundAuthorizationResult.Success);
    }

    private InboundAuthorizationResult.Denied? AuthorizeRequirement(
        InboundAuthorizationContext context,
        PermissionRequirement? requirement,
        string operationName)
    {
        if (requirement is null)
        {
            return null;
        }

        if (requirement.Value.Permission is null)
        {
            return null;
        }

        if (!context.User.IsAuthenticated)
        {
            logger.LogWarning(
                "Unauthenticated access to {HandlerType}/{OperationName}",
                context.HandlerType.DisplayName,
                operationName);
            return new InboundAuthorizationResult.Denied(
                Result.Fail("401", "Please login first"),
                StatusCode: 401,
                ChallengePermissions: GetPermissions(requirement.Value));
        }

        var requiredPermissions = GetPermissions(requirement.Value);
        if (requiredPermissions.Count == 0)
        {
            return null;
        }

        var grantedPermissions = ResolveGrantedPermissions(context.User);
        foreach (var permission in requiredPermissions)
        {
            if (HasPermission(grantedPermissions, permission))
            {
                continue;
            }

            logger.LogWarning(
                "Access denied to {HandlerType}/{OperationName} for user without permission {Permission}",
                context.HandlerType.DisplayName,
                operationName,
                permission);
            return new InboundAuthorizationResult.Denied(
                Result.Fail("403", "Insufficient permissions"),
                StatusCode: 403,
                ChallengePermissions: requiredPermissions);
        }

        return null;
    }

    private static string ResolveOperationName(InboundAuthorizationContext context, bool input)
        => context.Kind switch
        {
            InboundAuthorizationKind.Request when input => context.ServiceMethodInfo?.Name ?? context.HandlerMethodInfo.Name,
            InboundAuthorizationKind.Request => context.HandlerMethodInfo.Name,
            InboundAuthorizationKind.Command => context.MessageType?.DisplayName ?? context.HandlerMethodInfo.Name,
            InboundAuthorizationKind.Notification => context.MessageType?.DisplayName ?? context.HandlerMethodInfo.Name,
            _ => context.HandlerMethodInfo.Name
        };

    private static PermissionRequirement? ResolveInputRequirement(InboundAuthorizationContext context)
        => context.Kind switch
        {
            InboundAuthorizationKind.Request when context.ServiceType is not null && context.ServiceMethodInfo is not null
                => ResolveRequestRequirement(context.ServiceType, context.ServiceMethodInfo),
            InboundAuthorizationKind.Command when context.MessageType is not null
                => GetRequirement(context.MessageType),
            InboundAuthorizationKind.Notification when context.MessageType is not null
                => GetRequirement(context.MessageType),
            _ => null
        };

    private static PermissionRequirement? ResolveExecutionRequirement(InboundAuthorizationContext context)
        => GetRequirement(context.HandlerMethodInfo) ?? GetRequirement(context.HandlerType);

    private static PermissionRequirement? ResolveRequestRequirement(Type serviceType, MethodInfo serviceMethodInfo)
        => GetRequirement(serviceMethodInfo) ?? GetRequirement(serviceType);

    private static PermissionRequirement? GetRequirement(MemberInfo memberInfo)
    {
        var attr = memberInfo.GetCustomAttributes(inherit: true)
            .OfType<MetadataAttribute>()
            .LastOrDefault(static attr => string.Equals(attr.Key, RequirePermissionAttribute.MetadataKey, StringComparison.OrdinalIgnoreCase));

        return attr is null
            ? null
            : new PermissionRequirement(attr.Value, RequiresAllPermissions: false);
    }

    private static IReadOnlyCollection<string> ResolveGrantedPermissions(ClaimsPrincipal user)
        => user.FindAll(ClaimTypes.Permission)
            .Select(static claim => claim.Value)
            .Where(static permission => !string.IsNullOrWhiteSpace(permission))
            .Select(static permission => permission.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool HasPermission(IReadOnlyCollection<string> grantedPermissions, string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        if (grantedPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return grantedPermissions
            .Where(static pattern => pattern.Contains('*'))
            .Any(pattern => permission.MatchWildcard(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> GetPermissions(PermissionRequirement requirement)
        => requirement.RequiresAllPermissions
            ? requirement.Permission?.Split('\u001f', StringSplitOptions.RemoveEmptyEntries) ?? []
            : string.IsNullOrEmpty(requirement.Permission) ? [] : [requirement.Permission];

    private readonly record struct PermissionRequirement(string? Permission, bool RequiresAllPermissions);
}
