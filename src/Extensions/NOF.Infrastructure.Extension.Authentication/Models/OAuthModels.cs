using NOF.Contract.Extension.Authentication;
namespace NOF.Infrastructure.Extension.Authentication;

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
    string Scope,
    string? Nonce,
    string? CodeChallenge,
    string? CodeChallengeMethod);

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
