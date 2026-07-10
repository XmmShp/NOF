using Microsoft.Extensions.Logging;
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
    IMutableCurrentTenant currentTenant,
    IOptions<AuthenticationResourceServerOptions> options) :
    ICommandInboundMiddleware,
    INotificationInboundMiddleware,
    IRequestInboundMiddleware
{
    private const string WwwAuthenticateHeader = "WWW-Authenticate";
    private readonly AuthenticationResourceServerOptions _options = options.Value;

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

        using var trustedTenantScope = PushTrustedTenant();
        await next(context, message, cancellationToken);
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

        using var trustedTenantScope = PushTrustedTenant();
        await next(context, message, cancellationToken);
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

        using var trustedTenantScope = PushTrustedTenant();
        await next(context, request, cancellationToken);
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

    private IDisposable? PushTrustedTenant()
    {
        if (!userContext.User.IsAuthenticated)
        {
            return null;
        }

        var trustedTenantId = userContext.User.TenantId;
        if (string.IsNullOrWhiteSpace(trustedTenantId))
        {
            return null;
        }

        var tenantId = TenantId.Normalize(trustedTenantId);
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);
        return currentTenant.PushTenant(tenantId);
    }
}
