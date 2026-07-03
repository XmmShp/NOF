using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Resolves a service token from configured client credentials when the outbound context explicitly requests it.
/// </summary>
public sealed class ServiceTokenOutboundMiddleware(
    IClientCredentialsTokenService clientCredentialsTokenService,
    IOptions<AuthenticationResourceServerOptions> options,
    ILogger<ServiceTokenOutboundMiddleware> logger) :
    IRequestOutboundMiddleware,
    ICommandOutboundMiddleware,
    INotificationOutboundMiddleware
{
    public TopologyComparison Compare(IRequestOutboundMiddleware other)
        => other is Hosting.JwtTokenPropagationOutboundMiddleware
            ? TopologyComparison.After
            : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(ICommandOutboundMiddleware other)
        => other is JwtTokenPropagationOutboundMiddleware
            ? TopologyComparison.After
            : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(INotificationOutboundMiddleware other)
        => other is JwtTokenPropagationOutboundMiddleware
            ? TopologyComparison.After
            : TopologyComparison.DoesNotMatter;

    public async ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        await ApplyAsync(context, context.Headers, cancellationToken).ConfigureAwait(false);
        await next(context, request, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask InvokeAsync(CommandOutboundContext context, object message, CommandOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        await ApplyAsync(context, context.Headers, cancellationToken).ConfigureAwait(false);
        await next(context, message, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask InvokeAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        await ApplyAsync(context, context.Headers, cancellationToken).ConfigureAwait(false);
        await next(context, message, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ApplyAsync(Context context, IDictionary<string, string?> headers, CancellationToken cancellationToken)
    {
        var headerName = context.GetServiceTokenHeaderName();
        if (string.IsNullOrWhiteSpace(headerName))
        {
            return;
        }

        var tokenResponse = await clientCredentialsTokenService
            .GetTokenAsync(CreateTokenRequest(), cancellationToken)
            .ConfigureAwait(false);
        WriteHeader(headers, headerName, FormatHeaderValue(tokenResponse));
    }

    private ClientCredentialsTokenRequest CreateTokenRequest()
    {
        var credentials = options.Value.TokenExchangeClient;
        if (credentials is null
            || string.IsNullOrWhiteSpace(credentials.ClientId)
            || string.IsNullOrWhiteSpace(credentials.ClientSecret))
        {
            throw new InvalidOperationException("TokenExchangeClient credentials must be configured before using Context.WithServiceToken(...).");
        }

        return new ClientCredentialsTokenRequest
        {
            ClientId = credentials.ClientId,
            ClientSecret = credentials.ClientSecret
        };
    }

    private static string FormatHeaderValue(ClientCredentialsTokenResponse response)
        => string.IsNullOrWhiteSpace(response.TokenType)
            ? response.AccessToken
            : $"{response.TokenType} {response.AccessToken}";

    private void WriteHeader(IDictionary<string, string?> headers, string headerName, string headerValue)
    {
        if (headers.TryGetValue(headerName, out var existingValue)
            && !string.IsNullOrWhiteSpace(existingValue)
            && !string.Equals(existingValue, headerValue, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Service token outbound middleware is overwriting existing header '{HeaderName}'. Check outbound middleware ordering and explicit token configuration.",
                headerName);
        }

        headers[headerName] = headerValue;
    }
}
