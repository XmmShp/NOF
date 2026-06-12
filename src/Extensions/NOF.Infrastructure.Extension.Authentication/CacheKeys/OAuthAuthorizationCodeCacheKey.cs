using NOF.Application;

namespace NOF.Infrastructure.Extension.Authentication;

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
