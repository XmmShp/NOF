using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;
using System.Reflection;

namespace NOF.Infrastructure;

public sealed class AuthorizationInboundMiddleware(
    IUserContext userContext,
    ILogger<AuthorizationInboundMiddleware> logger) :
    ICommandInboundMiddleware,
    INotificationInboundMiddleware,
    IRequestInboundMiddleware
{
    private const string WwwAuthenticateHeader = "WWW-Authenticate";
    private const string BearerChallenge = "Bearer error=\"invalid_token\"";

    public TopologyComparison Compare(ICommandInboundMiddleware other)
        => other is TenantInboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(INotificationInboundMiddleware other)
        => other is TenantInboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(IRequestInboundMiddleware other)
        => other is TenantInboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public async ValueTask InvokeAsync(CommandInboundContext context, object message, CommandHandlerDelegate next, CancellationToken cancellationToken)
    {
        var requirement = ResolveMessageRequirement(context.MessageType, context.HandlerType, context.MethodInfo);
        if (Authorize(requirement, context.HandlerType.DisplayName, context.MessageType.DisplayName) is not null)
        {
            return;
        }

        await next(context, message, cancellationToken);
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, object message, NotificationHandlerDelegate next, CancellationToken cancellationToken)
    {
        var requirement = ResolveMessageRequirement(context.MessageType, context.HandlerType, context.MethodInfo);
        if (Authorize(requirement, context.HandlerType.DisplayName, context.MessageType.DisplayName) is not null)
        {
            return;
        }

        await next(context, message, cancellationToken);
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, object request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        var inputRequirement = ResolveRequestInputRequirement(context.ServiceType, context.ServiceMethodInfo);
        var executionRequirement = ResolveExecutionRequirement(context.HandlerType, context.HandlerMethodInfo);

        var inputFailure = Authorize(inputRequirement, context.ServiceType.DisplayName, context.ServiceMethodInfo.Name);
        if (inputFailure is not null)
        {
            ApplyFailure(context, inputFailure);
            return;
        }

        var executionFailure = Authorize(executionRequirement, context.HandlerType.DisplayName, context.HandlerMethodInfo.Name);
        if (executionFailure is not null)
        {
            ApplyFailure(context, executionFailure);
            return;
        }

        await next(context, request, cancellationToken);
    }

    private static PermissionRequirement? ResolveRequestInputRequirement(Type serviceType, MethodInfo serviceMethodInfo)
    {
        var typeRequirement = GetRequirement(serviceType);
        var methodRequirement = GetRequirement(serviceMethodInfo);
        return methodRequirement ?? typeRequirement;
    }

    private static PermissionRequirement? ResolveExecutionRequirement(Type handlerType, MethodInfo handlerMethodInfo)
    {
        var typeRequirement = GetRequirement(handlerType);
        var methodRequirement = GetRequirement(handlerMethodInfo);
        return methodRequirement ?? typeRequirement;
    }

    private static PermissionRequirement? ResolveMessageRequirement(Type messageType, Type handlerType, MethodInfo handlerMethodInfo)
    {
        var inputRequirement = GetRequirement(messageType);
        var executionRequirement = ResolveExecutionRequirement(handlerType, handlerMethodInfo);
        return Combine(inputRequirement, executionRequirement);
    }

    private static PermissionRequirement? Combine(PermissionRequirement? inputRequirement, PermissionRequirement? executionRequirement)
        => (inputRequirement, executionRequirement) switch
        {
            (null, null) => null,
            (null, var execution) => execution,
            (var input, null) => input,
            (var input, var execution) when input.Value.Permission is null => execution,
            (var input, var execution) when execution.Value.Permission is null => input,
            (var input, var execution) when string.Equals(input.Value.Permission, execution.Value.Permission, StringComparison.Ordinal) => input,
            (var input, var execution) => new PermissionRequirement(string.Join('\u001f', input.Value.Permission, execution.Value.Permission), RequiresAllPermissions: true)
        };

    private static PermissionRequirement? GetRequirement(MemberInfo memberInfo)
    {
        var attr = memberInfo.GetCustomAttributes(inherit: true)
            .OfType<MetadataAttribute>()
            .LastOrDefault(static attr => string.Equals(attr.Key, RequirePermissionAttribute.MetadataKey, StringComparison.OrdinalIgnoreCase));

        return attr is null
            ? null
            : new PermissionRequirement(attr.Value, RequiresAllPermissions: false);
    }

    private IResult? Authorize(PermissionRequirement? requirement, string handlerName, string messageName)
    {
        if (requirement is null || requirement.Value.Permission is null)
        {
            return null;
        }

        if (!userContext.User.IsAuthenticated)
        {
            logger.LogWarning("Unauthenticated access to {HandlerType}/{MessageType}", handlerName, messageName);
            return Result.Fail("401", "Please login first");
        }

        if (requirement.Value.Permission.Length == 0)
        {
            logger.LogDebug("Authorization passed for {HandlerType}/{MessageType}; authenticated user required", handlerName, messageName);
            return null;
        }

        var permissions = requirement.Value.RequiresAllPermissions
            ? requirement.Value.Permission.Split('\u001f', StringSplitOptions.RemoveEmptyEntries)
            : [requirement.Value.Permission];

        foreach (var permission in permissions)
        {
            if (!userContext.User.HasPermission(permission))
            {
                logger.LogWarning("Access denied to {HandlerType}/{MessageType} for user without permission {Permission}",
                    handlerName, messageName, permission);
                return Result.Fail("403", "Insufficient permissions");
            }
        }

        logger.LogDebug("Authorization passed for {HandlerType}/{MessageType}", handlerName, messageName);
        return null;
    }

    private static void ApplyFailure(RequestInboundContext context, IResult failure)
    {
        if (failure.ErrorCode == "401")
        {
            context.ResponseHeaders[WwwAuthenticateHeader] = BearerChallenge;
        }

        context.SetResponse(failure, ignoreResultResponseType: false);
    }

    private readonly record struct PermissionRequirement(string? Permission, bool RequiresAllPermissions);
}
