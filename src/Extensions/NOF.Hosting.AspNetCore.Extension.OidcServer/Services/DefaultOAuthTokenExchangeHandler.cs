using System.Security.Claims;
using System.Text.Json;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthTokenExchangeHandler : IOAuthTokenExchangeHandler
{
    public ValueTask<OAuthTokenExchangeResult> HandleAsync(
        OAuthTokenExchangeRequest request,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var grantedScopes = ParseScopes(request.SubjectPrincipal.FindFirst(OAuthClaimTypes.Scope)?.Value ?? string.Empty);
        var actorScopes = ParseScopes(request.ActorPrincipal.FindFirst(OAuthClaimTypes.Scope)?.Value ?? string.Empty);
        var scopes = grantedScopes.Where(actorScopes.Contains).ToHashSet(StringComparer.Ordinal);
        if (request.RequestedScopes.Count > 0)
        {
            scopes.IntersectWith(request.RequestedScopes);
        }

        if (scopes.Count == 0)
        {
            return ValueTask.FromResult<OAuthTokenExchangeResult>(
                new OAuthTokenExchangeResult.Failure(
                    "invalid_scope",
                    "requested scope is not granted by subject_token and actor_token."));
        }

        var actorSubject = request.ActorPrincipal.FindFirst(OAuthClaimTypes.Subject)?.Value;
        if (string.IsNullOrWhiteSpace(actorSubject))
        {
            return ValueTask.FromResult<OAuthTokenExchangeResult>(
                new OAuthTokenExchangeResult.Failure(
                    "invalid_grant",
                    "actor_token subject is invalid."));
        }

        TokenClaim[]? accessTokenClaims = request.ClientType == OAuthClientType.Confidential
            ? [TokenClaim.Json(OAuthClaimTypes.Actor, BuildActorClaim(actorSubject, ResolveNestedActor(request.ActorPrincipal)))]
            : null;

        return ValueTask.FromResult<OAuthTokenExchangeResult>(
            new OAuthTokenExchangeResult.Success(
                request.Subject,
                scopes,
                accessTokenClaims));
    }

    private static string BuildActorClaim(string subject, JsonElement? nestedActor)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString(OAuthClaimTypes.Subject, subject);
            if (nestedActor is { } nested)
            {
                writer.WritePropertyName(OAuthClaimTypes.Actor);
                nested.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static JsonElement? ResolveNestedActor(ClaimsPrincipal actorPrincipal)
    {
        var actorJson = actorPrincipal.FindFirst(OAuthClaimTypes.Actor)?.Value;
        if (string.IsNullOrWhiteSpace(actorJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(actorJson);
        return document.RootElement.Clone();
    }

    private static IReadOnlySet<string> ParseScopes(string scope)
        => scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
}
