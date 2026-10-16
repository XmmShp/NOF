using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;

namespace NOF.Infrastructure;

public sealed class AuthorizationInboundMiddleware(
    IUserContext userContext,
    IInboundAuthorizationHandler authorizationHandler,
    IOptions<AuthenticationResourceServerOptions> options,
    IOptions<TenantAuthorizationOptions> tenantOptions) :
    ICommandInboundMiddleware,
    INotificationInboundMiddleware,
    IRequestInboundMiddleware
{
    private const string WwwAuthenticateHeader = "WWW-Authenticate";
    private readonly AuthenticationResourceServerOptions _options = options.Value;
    private readonly TenantAuthorizationOptions _tenantOptions = tenantOptions.Value;

    /// <summary>Creates authorization middleware with the default tenant trust policy.</summary>
    public AuthorizationInboundMiddleware(IUserContext userContext,
        IInboundAuthorizationHandler authorizationHandler,
        IOptions<AuthenticationResourceServerOptions> options)
        : this(userContext, authorizationHandler, options, Microsoft.Extensions.Options.Options.Create(new TenantAuthorizationOptions()))
    {
    }

    public TopologyComparison Compare(ICommandInboundMiddleware other)
        => other is TenantInboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(INotificationInboundMiddleware other)
        => other is TenantInboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(IRequestInboundMiddleware other)
        => other is TenantInboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public async ValueTask InvokeAsync(CommandInboundContext context, object message, CommandHandlerDelegate next, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationHandler.AuthorizeAsync(
            new InboundAuthorizationContext
            {
                Kind = InboundAuthorizationKind.Command,
                User = userContext.User,
                ExecutionContext = context,
                Input = message,
                HandlerType = context.HandlerType,
                HandlerMethodInfo = context.MethodInfo,
                MessageType = context.MessageType
            },
            cancellationToken).ConfigureAwait(false);
        if (authorizationResult is InboundAuthorizationResult.Denied)
        {
            return;
        }

        await next(WithTrustedTenant(context), message, cancellationToken);
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, object message, NotificationHandlerDelegate next, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationHandler.AuthorizeAsync(
            new InboundAuthorizationContext
            {
                Kind = InboundAuthorizationKind.Notification,
                User = userContext.User,
                ExecutionContext = context,
                Input = message,
                HandlerType = context.HandlerType,
                HandlerMethodInfo = context.MethodInfo,
                MessageType = context.MessageType
            },
            cancellationToken).ConfigureAwait(false);
        if (authorizationResult is InboundAuthorizationResult.Denied)
        {
            return;
        }

        await next(WithTrustedTenant(context), message, cancellationToken);
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, object request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationHandler.AuthorizeAsync(
            new InboundAuthorizationContext
            {
                Kind = InboundAuthorizationKind.Request,
                User = userContext.User,
                ExecutionContext = context,
                Input = request,
                HandlerType = context.HandlerType,
                HandlerMethodInfo = context.HandlerMethodInfo,
                ServiceType = context.ServiceType,
                ServiceMethodInfo = context.ServiceMethodInfo,
            },
            cancellationToken).ConfigureAwait(false);

        if (authorizationResult is InboundAuthorizationResult.Denied denied)
        {
            ApplyFailure(context, denied);
            return;
        }

        if (ValidateRequestTenant(context) is { } tenantFailure)
        {
            ApplyFailure(context, tenantFailure);
            return;
        }

        await next(WithTrustedTenant(context), request, cancellationToken);
    }

    private InboundAuthorizationResult.Denied? ValidateRequestTenant(RequestInboundContext context)
    {
        var isHostOperation = UseHostTenantAttribute.IsRequired(context.ServiceMethodInfo);
        var isAuthenticated = userContext.User.IsAuthenticated;
        var trustedTenant = isAuthenticated ? userContext.User.TenantId : null;
        var normalizedTenant = TenantId.Normalize(trustedTenant);
        if (_tenantOptions.RequireAuthenticatedTenantForRequests)
        {
            if (!isAuthenticated)
            {
                return new(Result.Fail("401", "Please login first"), 401, []);
            }

            if (!isHostOperation && (string.IsNullOrWhiteSpace(trustedTenant)
                || normalizedTenant == NOFAbstractionConstants.Tenant.HostId))
            {
                return new(Result.Fail("403", "An authenticated tenant is required"), 403, []);
            }
        }

        if (_tenantOptions.RejectMismatchedTenantForRequests && !isHostOperation
            && !string.IsNullOrWhiteSpace(trustedTenant)
            && context.TenantId != NOFAbstractionConstants.Tenant.HostId
            && !string.Equals(context.TenantId, normalizedTenant, StringComparison.Ordinal))
        {
            return new(Result.Fail("403", "Request tenant does not match the authenticated tenant"), 403, []);
        }

        return null;
    }

    private void ApplyFailure(RequestInboundContext context, InboundAuthorizationResult.Denied failure)
    {
        context.ResponseHeaders[NOFInfrastructureConstants.Transport.Headers.HttpStatusCode] =
            failure.StatusCode.ToString(CultureInfo.InvariantCulture);
        context.ResponseHeaders[WwwAuthenticateHeader] = CreateBearerChallenge(failure);
        context.SetResponse(failure.Result, ignoreResultResponseType: false);
    }

    private string CreateBearerChallenge(InboundAuthorizationResult.Denied failure)
    {
        var parameters = new List<string>
        {
            failure.StatusCode == 403
                ? "error=\"insufficient_scope\""
                : "error=\"invalid_token\""
        };

        if (!string.IsNullOrWhiteSpace(_options.AuthorizationServerIssuer))
        {
            parameters.Add($"authorization_server=\"{EscapeChallengeValue(_options.AuthorizationServerIssuer)}\"");
        }

        if (failure.StatusCode == 403)
        {
            var scope = string.Join(' ', failure.ChallengePermissions.Where(static permission => !string.IsNullOrWhiteSpace(permission)));
            if (!string.IsNullOrWhiteSpace(scope))
            {
                parameters.Add($"scope=\"{EscapeChallengeValue(scope)}\"");
            }
        }

        return $"Bearer {string.Join(", ", parameters)}";
    }

    private static string EscapeChallengeValue(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private TContext WithTrustedTenant<TContext>(TContext context)
        where TContext : Context
    {
        if (!userContext.User.IsAuthenticated)
        {
            return context;
        }

        var trustedTenantId = userContext.User.TenantId;
        if (string.IsNullOrWhiteSpace(trustedTenantId))
        {
            return context;
        }

        var tenantId = TenantId.Normalize(trustedTenantId);
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);
        return (TContext)context.WithTenantId(tenantId);
    }
}
