using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed record TokenClaim
{
    public TokenClaim()
    {
    }

    public TokenClaim(string type, string value)
    {
        Type = type;
        Value = value;
    }

    public TokenClaim(string type, string value, string? valueType)
        : this(type, value)
    {
        ValueType = valueType;
    }

    public string Type { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Values { get; set; }

    public string? ValueType { get; set; }

    public static TokenClaim Integer64(string type, long value)
    {
        return new TokenClaim(type, value.ToString(), ClaimValueTypes.Integer64);
    }

    public static TokenClaim Array(string type, params string[] values)
    {
        return new TokenClaim
        {
            Type = type,
            Values = values
        };
    }

    public static TokenClaim Json(string type, string value)
    {
        return new TokenClaim(type, value, JsonClaimValueTypes.Json);
    }

    public static TokenClaim Json(string type, JsonElement value)
    {
        return Json(type, value.GetRawText());
    }
}

public sealed class RefreshTokenOptions
{
    public TimeSpan Expiration { get; set; }

    public TokenClaim[]? Claims { get; set; }
}

public sealed class IssuedRefreshToken
{
    public required string Token { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
}

public record IssueTokenRequest
{
    public required string Audience { get; set; }

    public required string ClientId { get; set; }

    public TimeSpan AccessTokenExpiration { get; set; }

    public TokenClaim[]? AccessClaims { get; set; }

    public RefreshTokenOptions? RefreshToken { get; set; }
}

public record IssueTokenResponse
{
    public required string AccessToken { get; set; }

    public required DateTime AccessTokenExpiresAtUtc { get; set; }

    public IssuedRefreshToken? RefreshToken { get; set; }
}

public record RevokeRefreshTokenRequest
{
    public required string TokenId { get; set; }

    public TimeSpan Expiration { get; set; }
}

public record ValidateRefreshTokenRequest
{
    public required string RefreshToken { get; set; }

    public string? Audience { get; set; }
}

public record ValidateRefreshTokenResponse
{
    public required string TokenId { get; set; }

    public required TokenClaim[] Claims { get; set; }
}

public record IntrospectTokenRequest
{
    public required string Token { get; set; }

    public string? TokenTypeHint { get; set; }

    public string? Audience { get; set; }
}

public record IntrospectTokenResponse
{
    public required bool Active { get; set; }

    public string? TokenType { get; set; }

    public TokenClaim[] Claims { get; set; } = [];
}

public sealed class ManagedSigningKey
{
    public required string Kid { get; init; }

    public required RsaSecurityKey Key { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime ActivatedAtUtc { get; init; }
}

public sealed record OAuthAuthorizationRequest(
    string ResponseType,
    string ClientId,
    string RedirectUri,
    string Scope,
    string State,
    string? Nonce,
    string? CodeChallenge,
    string? CodeChallengeMethod);

public sealed record OAuthAuthorizationCodeDescriptor(
    string Subject,
    string ClientId,
    string RedirectUri,
    bool WasRedirectUriSupplied,
    string Scope,
    string? Nonce,
    string? CodeChallenge,
    string? CodeChallengeMethod);

public sealed record OAuthTokenExchangeRequest(
    string ClientId,
    OAuthClientType ClientType,
    string Subject,
    ClaimsPrincipal SubjectPrincipal,
    ClaimsPrincipal ActorPrincipal,
    IReadOnlySet<string> RequestedScopes);

public abstract record OAuthTokenExchangeResult
{
    public sealed record Success(
        string Subject,
        IReadOnlySet<string> Scopes,
        TokenClaim[]? AccessTokenClaims = null) : OAuthTokenExchangeResult;

    public sealed record Failure(string Error, string ErrorDescription) : OAuthTokenExchangeResult;
}

public sealed record OAuthSubjectProfile(
    string Subject,
    IReadOnlyList<KeyValuePair<string, string>> AccessTokenClaims,
    IReadOnlyList<KeyValuePair<string, string>> IdentityClaims)
{
    public static OAuthSubjectProfile Create(
        string subject,
        IEnumerable<KeyValuePair<string, string>>? accessTokenClaims = null,
        IEnumerable<KeyValuePair<string, string>>? identityClaims = null)
    {
        var normalizedAccessClaims = NormalizeClaims(accessTokenClaims);
        var normalizedIdentityClaims = NormalizeClaims(identityClaims);

        return new OAuthSubjectProfile(
            subject,
            EnsureSubject(normalizedAccessClaims, subject),
            EnsureSubject(normalizedIdentityClaims, subject));
    }

    private static IReadOnlyList<KeyValuePair<string, string>> NormalizeClaims(IEnumerable<KeyValuePair<string, string>>? claims) =>
        claims?
            .Where(static claim => !string.IsNullOrWhiteSpace(claim.Key) && claim.Value is not null)
            .ToArray()
        ?? [];

    private static IReadOnlyList<KeyValuePair<string, string>> EnsureSubject(
        IReadOnlyList<KeyValuePair<string, string>> claims,
        string subject)
    {
        if (claims.Any(static claim => string.Equals(claim.Key, OAuthClaimTypes.Subject, StringComparison.Ordinal)))
        {
            return claims;
        }

        return [new(OAuthClaimTypes.Subject, subject), .. claims];
    }
}

public abstract record OAuthAuthorizationResult
{
    public sealed record Authorized(string Subject) : OAuthAuthorizationResult;

    public sealed record Challenge(string RedirectUrl) : OAuthAuthorizationResult;

    public sealed record Failure(string Error, string ErrorDescription) : OAuthAuthorizationResult;
}
