using NOF.Abstraction;
using NOF.Hosting;

namespace NOF.Infrastructure.Extension.Authentication;

/// <summary>Propagates access tokens to outbound commands and notifications.</summary>
public sealed class AccessTokenPropagationOutboundMiddleware :
    ICommandOutboundMiddleware,
    INotificationOutboundMiddleware
{
    public TopologyComparison Compare(ICommandOutboundMiddleware other) => TopologyComparison.DoesNotMatter;
    public TopologyComparison Compare(INotificationOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

    private readonly IUserContext _userContext;

    public AccessTokenPropagationOutboundMiddleware(IUserContext userContext)
    {
        _userContext = userContext;
    }

    public ValueTask InvokeAsync(CommandOutboundContext context, object message, CommandOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        Propagate(context.Headers);
        return next(context, message, cancellationToken);
    }

    public ValueTask InvokeAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        Propagate(context.Headers);
        return next(context, message, cancellationToken);
    }

    private void Propagate(IDictionary<string, string?> headers)
    {
        foreach (var identity in _userContext.User.GetIdentities<JwtClaimsIdentity>()
            .Where(identity => identity.DownstreamPropagation is not null)
            .Where(identity => identity.Token.Length > 0))
        {
            var propagation = identity.DownstreamPropagation!;
            headers[propagation.HeaderName] = FormatHeaderValue(propagation, identity.Token);
        }
    }

    private static string FormatHeaderValue(JwtPropagation propagation, string token)
        => string.IsNullOrEmpty(propagation.TokenType)
            ? token
            : $"{propagation.TokenType} {token}";
}
