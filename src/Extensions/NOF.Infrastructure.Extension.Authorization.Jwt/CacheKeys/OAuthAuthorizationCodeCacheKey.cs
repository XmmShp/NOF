using NOF.Application;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

internal sealed record OAuthAuthorizationCodeCacheKey(string Code)
    : CacheKey<OAuthAuthorizationCodeCacheValue>($"nof:oauth:auth-code:{Code}");

internal sealed record OAuthAuthorizationCodeCacheValue
{
    public required string Subject { get; init; }

    public required string ClientId { get; init; }

    public required string RedirectUri { get; init; }

    public required string Scope { get; init; }

    public string? Nonce { get; init; }

    public string? CodeChallenge { get; init; }

    public string? CodeChallengeMethod { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }
}

internal sealed record OAuthRedeemedAuthorizationCodeCacheKey(string Code)
    : CacheKey<OAuthRedeemedAuthorizationCodeCacheValue>($"nof:oauth:auth-code:redeemed:{Code}");

internal sealed record OAuthRedeemedAuthorizationCodeCacheValue
{
    public required string ClientId { get; init; }

    public required string RedirectUri { get; init; }

    public required OAuthTokenEndpointResponse Response { get; init; }
}
