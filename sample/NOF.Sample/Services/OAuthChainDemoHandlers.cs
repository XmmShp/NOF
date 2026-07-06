using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using NOF.Hosting.AspNetCore.Extension.OidcServer;
using System.Security.Claims;
using System.Text.Json;

namespace NOF.Sample.Services;

internal static class OAuthChainDemoContextKeys
{
    public static readonly object AccessToken = new();
}

public sealed class CreateClientHandler(OAuthChainDemoBackend backend) : OAuthChainDemoService.CreateClient
{
    public override Task<Result<CreateDemoOAuthClientResponse>> HandleAsync(
        CreateDemoOAuthClientRequest request,
        Context context,
        CancellationToken cancellationToken)
    {
        _ = context;
        return backend.CreateClientAsync(request, cancellationToken);
    }
}

public sealed class GetClientTokenHandler(OAuthChainDemoBackend backend) : OAuthChainDemoService.GetClientToken
{
    public override Task<Result<DemoTokenResponse>> HandleAsync(
        GetDemoClientTokenRequest request,
        Context context,
        CancellationToken cancellationToken)
    {
        _ = context;
        return backend.GetClientTokenAsync(request, cancellationToken);
    }
}

public sealed class GetUserTokenHandler(OAuthChainDemoBackend backend) : OAuthChainDemoService.GetUserToken
{
    public override Task<Result<DemoTokenResponse>> HandleAsync(
        GetDemoUserTokenRequest request,
        Context context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        return backend.GetUserTokenAsync(cancellationToken);
    }
}

public sealed class ExchangeTokenHandler(OAuthChainDemoBackend backend) : OAuthChainDemoService.ExchangeToken
{
    public override Task<Result<DemoTokenResponse>> HandleAsync(
        ExchangeDemoTokenRequest request,
        Context context,
        CancellationToken cancellationToken)
    {
        _ = context;
        return backend.ExchangeTokenAsync(request, cancellationToken);
    }
}

public sealed class CallDownstreamHandler(IDemoDownstreamServiceClient downstreamClient) : OAuthChainDemoService.CallDownstream
{
    public override Task<Result<ConsumeDemoAccessTokenResponse>> HandleAsync(
        CallDemoDownstreamRequest request,
        Context context,
        CancellationToken cancellationToken)
    {
        _ = context;
        var downstreamContext = Context.Empty.WithItem(OAuthChainDemoContextKeys.AccessToken, request.AccessToken);
        return downstreamClient.InspectAccessTokenAsync(Empty.Instance, downstreamContext, cancellationToken);
    }
}

public sealed class InspectAccessTokenHandler(IUserContext userContext) : DemoDownstreamService.InspectAccessToken
{
    public override Task<Result<ConsumeDemoAccessTokenResponse>> HandleAsync(
        Empty request,
        Context context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        if (!userContext.User.IsAuthenticated)
        {
            return Task.FromResult<Result<ConsumeDemoAccessTokenResponse>>(Result.Fail("401", "Access token is required."));
        }

        var scopes = userContext.User.FindAll("scope")
            .SelectMany(static claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        var actor = ResolveActor(userContext.User);

        return Task.FromResult<Result<ConsumeDemoAccessTokenResponse>>(new ConsumeDemoAccessTokenResponse
        {
            IsAuthenticated = true,
            Subject = userContext.User.FindFirst("sub")?.Value,
            TenantId = userContext.User.TenantId,
            ActorSubject = actor?.Subject,
            ActorJson = actor?.Json,
            Permissions = userContext.User.Permissions.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            Scopes = scopes
        });
    }

    private static (string? Subject, string Json)? ResolveActor(ClaimsPrincipal user)
    {
        var actorJson = user.FindFirst(OAuthClaimTypes.Actor)?.Value;
        if (string.IsNullOrWhiteSpace(actorJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(actorJson);
        var root = document.RootElement;
        var subject = root.TryGetProperty(OAuthClaimTypes.Subject, out var subjectElement)
            ? subjectElement.GetString()
            : null;

        return string.IsNullOrWhiteSpace(subject)
            ? null
            : (subject, actorJson);
    }
}

public sealed class OAuthChainDemoAccessTokenOutboundMiddleware : IRequestOutboundMiddleware
{
    public TopologyComparison Compare(IRequestOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

    public ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        _ = request;
        if (context.TryGetItem(OAuthChainDemoContextKeys.AccessToken, out var tokenObject)
            && tokenObject is string token
            && !string.IsNullOrWhiteSpace(token)
            && !context.Headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.Authorization))
        {
            context.Headers[NOFAbstractionConstants.Transport.Headers.Authorization] = $"Bearer {token}";
        }

        return next(context, request, cancellationToken);
    }
}
