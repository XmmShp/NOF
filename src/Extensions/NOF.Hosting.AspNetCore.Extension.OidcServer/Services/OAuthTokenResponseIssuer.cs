using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

internal sealed class OAuthTokenResponseIssuer(
    IOAuthSubjectService subjectService,
    ITokenService tokenService,
    ISigningKeyService signingKeyService,
    IOptions<OAuthAuthorizationServerOptions> oauthOptions)
{
    public async ValueTask<OAuthTokenEndpointResponse?> IssueAsync(
        string subject,
        string scope,
        string clientId,
        string? idTokenAudience,
        string? nonce,
        IReadOnlyList<TokenClaim>? additionalAccessClaims,
        bool issueRefreshToken,
        CancellationToken cancellationToken)
    {
        var options = oauthOptions.Value;
        var scopes = ParseScopes(scope);
        var profile = await subjectService.GetProfileAsync(subject, scopes, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return null;
        }

        var accessClaims = profile.AccessTokenClaims
            .Select(static claim => new TokenClaim(claim.Key, claim.Value))
            .ToList();
        accessClaims.Add(new TokenClaim(OAuthClaimTypes.Scope, string.Join(' ', scopes)));
        if (additionalAccessClaims is not null)
        {
            accessClaims.AddRange(additionalAccessClaims);
        }

        var issueResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = options.AccessTokenAudience,
                AccessTokenExpiration = options.AccessTokenExpiration,
                AccessClaims = accessClaims.ToArray(),
                ClientId = clientId,
                RefreshToken = issueRefreshToken
                    ? new RefreshTokenOptions
                    {
                        Expiration = options.RefreshTokenExpiration,
                        Claims =
                        [
                            new TokenClaim(OAuthClaimTypes.Subject, subject),
                            new TokenClaim(OAuthClaimTypes.Scope, string.Join(' ', scopes)),
                            new TokenClaim(OAuthClaimTypes.ClientId, clientId)
                        ]
                    }
                    : null
            },
            cancellationToken).ConfigureAwait(false);
        if (!issueResult.IsSuccess || (issueRefreshToken && issueResult.Value.RefreshToken is null))
        {
            return null;
        }

        string? idToken = null;
        if (!string.IsNullOrWhiteSpace(idTokenAudience) && scopes.Contains(OAuthScope.OpenId))
        {
            var idTokenClaims = profile.IdentityClaims
                .Where(claim => ShouldEmitIdentityClaim(claim.Key, scopes))
                .Select(claim => claim.Key == OAuthClaimTypes.IssuedAt && long.TryParse(claim.Value, out _)
                    ? new Claim(claim.Key, claim.Value, ClaimValueTypes.Integer64)
                    : new Claim(claim.Key, claim.Value))
                .ToList();
            if (idTokenClaims.All(static claim => claim.Type != OAuthClaimTypes.Subject))
            {
                idTokenClaims.Insert(0, new Claim(OAuthClaimTypes.Subject, profile.Subject));
            }

            idTokenClaims.Add(new Claim(
                OAuthClaimTypes.IssuedAt,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64));

            if (!string.IsNullOrWhiteSpace(nonce))
            {
                idTokenClaims.Add(new Claim(OAuthClaimTypes.Nonce, nonce));
            }

            var now = DateTime.UtcNow;
            var signingKey = (await signingKeyService.GetCurrentSigningKeyAsync(cancellationToken).ConfigureAwait(false)).Key;
            var token = new JwtSecurityToken(
                issuer: options.Issuer,
                audience: idTokenAudience,
                claims: idTokenClaims,
                notBefore: now,
                expires: now.Add(options.AccessTokenExpiration),
                signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));
            token.Header["typ"] = "JWT";

            idToken = new JwtSecurityTokenHandler().WriteToken(token);
        }

        return new OAuthTokenEndpointResponse
        {
            AccessToken = issueResult.Value.AccessToken,
            TokenType = "Bearer",
            ExpiresIn = (long)options.AccessTokenExpiration.TotalSeconds,
            RefreshToken = issueResult.Value.RefreshToken?.Token,
            Scope = string.Join(' ', scopes),
            IdToken = idToken
        };
    }

    private static IReadOnlySet<string> ParseScopes(string scope)
        => scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    private static bool ShouldEmitIdentityClaim(string claimType, IReadOnlySet<string> scopes)
    {
        return claimType switch
        {
            OAuthClaimTypes.Email or OAuthClaimTypes.EmailVerified => scopes.Contains(OAuthScope.Email),
            OAuthClaimTypes.Name or OAuthClaimTypes.Groups => scopes.Contains(OAuthScope.Profile),
            OAuthClaimTypes.Scope or OAuthClaimTypes.SessionId => false,
            _ => true
        };
    }
}
