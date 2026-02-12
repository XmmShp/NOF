using Microsoft.Extensions.Options;
using NOF.Application;
using System.Diagnostics;

namespace NOF.Infrastructure.Core;

/// <summary>Invocation context step — JWT validation, tenant resolution, tracing.</summary>
public class InvocationContextMiddlewareStep : IInboundMiddlewareStep<InvocationContextMiddleware>, IAfter<ExceptionMiddlewareStep>;

/// <summary>
/// Handler middleware that populates the <see cref="IInvocationContext"/> from
/// transport-agnostic <see cref="InboundContext.Headers"/>.
/// <para>
/// Responsibilities:
/// <list type="bullet">
///   <item><description>JWT validation → <see cref="IInvocationContext.User"/></description></item>
///   <item><description>Tenant resolution → <see cref="IInvocationContext.TenantId"/></description></item>
///   <item><description>Tracing propagation → <see cref="IInvocationContext.TraceId"/>/<see cref="IInvocationContext.SpanId"/></description></item>
/// </list>
/// Authorization is handled separately by <see cref="PermissionAuthorizationMiddleware"/>.
/// </para>
/// </summary>
public sealed class InvocationContextMiddleware : IInboundMiddleware
{
    private readonly IInvocationContextInternal _invocationContext;
    private readonly IJwtValidationService? _jwtValidationService;
    private readonly AuthorizationOptions _authOptions;

    public InvocationContextMiddleware(
        IInvocationContextInternal invocationContext,
        IOptions<AuthorizationOptions> authOptions,
        IJwtValidationService? jwtValidationService = null)
    {
        _invocationContext = invocationContext;
        _authOptions = authOptions.Value;
        _jwtValidationService = jwtValidationService;
    }

    public async ValueTask InvokeAsync(InboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        // 1. Populate identity: parse JWT Bearer token from headers
        await PopulateIdentityAsync(context, cancellationToken);

        // 2. Resolve tenant: JWT claims first, then header fallback
        ResolveTenant(context);

        // 3. Resolve tracing info from headers
        ResolveTracing(context);

        await next(cancellationToken);
    }

    private async Task PopulateIdentityAsync(InboundContext context, CancellationToken cancellationToken)
    {
        // Try to extract Bearer token from Authorization header
        if (context.Headers.TryGetValue(_authOptions.HeaderName, out var authHeader) &&
            !string.IsNullOrEmpty(authHeader))
        {
            var prefix = _authOptions.TokenType + " ";
            var token = authHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? authHeader[prefix.Length..]
                : authHeader;

            if (_jwtValidationService is not null && !string.IsNullOrEmpty(token))
            {
                var managedUser = await _jwtValidationService.ValidateTokenAsync(token, cancellationToken);
                if (managedUser is not null)
                {
                    _invocationContext.SetUser(managedUser);
                    return;
                }
            }
        }

        _invocationContext.UnsetUser();
    }

    private void ResolveTenant(InboundContext context)
    {
        // Prioritize tenant from JWT claims; fall back to header
        string? tenantId = null;

        if (_invocationContext.User.IsAuthenticated)
        {
            tenantId = _invocationContext.User.Principal
                .FindFirst(NOFInfrastructureCoreConstants.Jwt.ClaimTypes.TenantId)?.Value;
        }

        if (string.IsNullOrEmpty(tenantId) &&
            context.Headers.TryGetValue(NOFInfrastructureCoreConstants.Transport.Headers.TenantId, out var headerTenantId) &&
            !string.IsNullOrEmpty(headerTenantId))
        {
            tenantId = headerTenantId;
        }

        _invocationContext.SetTenantId(tenantId);

        if (!string.IsNullOrEmpty(tenantId))
        {
            var activity = Activity.Current;
            if (activity is { IsAllDataRequested: true })
            {
                activity.SetTag(NOFInfrastructureCoreConstants.InboundPipeline.Tags.TenantId, tenantId);
            }
        }
    }

    private void ResolveTracing(InboundContext context)
    {
        context.Headers.TryGetValue(NOFInfrastructureCoreConstants.Transport.Headers.TraceId, out var traceId);
        context.Headers.TryGetValue(NOFInfrastructureCoreConstants.Transport.Headers.SpanId, out var spanId);
        _invocationContext.SetTracingInfo(traceId, spanId);
    }
}
