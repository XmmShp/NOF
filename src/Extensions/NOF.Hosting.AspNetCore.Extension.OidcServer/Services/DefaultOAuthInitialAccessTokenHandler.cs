using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NOF.Contract;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthInitialAccessTokenHandler(
    ITokenService tokenService,
    IOptions<OAuthAuthorizationServerOptions> options) : IOAuthInitialAccessTokenHandler
{
    public async Task<Result> HandleAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryReadBearerToken(request, out var token))
        {
            return Result.Fail("invalid_token", "an initial access token is required.");
        }

        var introspection = await tokenService.IntrospectTokenAsync(
            new IntrospectTokenRequest
            {
                Token = token,
                TokenTypeHint = OAuthTokenTypes.AccessToken,
                Audience = options.Value.AccessTokenAudience
            },
            cancellationToken).ConfigureAwait(false);
        if (!introspection.IsSuccess || !introspection.Value.Active)
        {
            return Result.Fail("invalid_token", "initial access token is invalid.");
        }

        var requiredScope = options.Value.ClientRegistrationInitialAccessTokenScope;
        var scopes = introspection.Value.Claims
            .Where(static claim => string.Equals(claim.Type, OAuthClaimTypes.Scope, StringComparison.Ordinal))
            .SelectMany(static claim => claim.Values ?? [claim.Value ?? string.Empty])
            .SelectMany(static value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);
        return string.IsNullOrWhiteSpace(requiredScope) || scopes.Contains(requiredScope)
            ? Result.Success()
            : Result.Fail("insufficient_scope", $"initial access token must contain the '{requiredScope}' scope.");
    }

    internal static bool TryReadBearerToken(HttpRequest request, out string token)
    {
        token = string.Empty;
        var authorization = request.Headers.Authorization.ToString().Trim();
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = authorization[prefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }
}
