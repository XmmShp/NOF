using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NOF.Infrastructure;
using OidcRoutes = Microsoft.AspNetCore.Routing.NOFOidcServerExtensions;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthUserInfoEndpoint(
    IOAuthSubjectService subjectService,
    ISigningKeyService signingKeyService,
    IOptions<OAuthAuthorizationServerOptions> oauthOptions) : IOAuthUserInfoEndpoint
{
    public async Task<IResult> HandleAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var accessToken = await OidcRoutes.ResolveBearerTokenAsync(request, cancellationToken).ConfigureAwait(false);
        var principal = await OidcRoutes.ValidateAccessTokenAsync(
            accessToken,
            signingKeyService,
            oauthOptions.Value,
            oauthOptions.Value.AccessTokenAudience,
            cancellationToken).ConfigureAwait(false);
        var subject = principal?.FindFirst(OAuthClaimTypes.Subject)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return OidcRoutes.CreateOAuthErrorResult(
                "invalid_token",
                "access token is invalid.",
                StatusCodes.Status401Unauthorized);
        }

        var scope = principal!.FindFirst(OAuthClaimTypes.Scope)?.Value ?? string.Empty;
        var scopes = OidcRoutes.ParseScopes(scope);
        var profile = await subjectService.GetProfileAsync(subject, scopes, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return OidcRoutes.CreateOAuthErrorResult(
                "invalid_token",
                "access token subject is invalid.",
                StatusCodes.Status401Unauthorized);
        }

        IReadOnlyDictionary<string, object> claims = profile.IdentityClaims
            .Where(claim => OidcRoutes.ShouldEmitIdentityClaim(claim.Key, scopes))
            .GroupBy(static claim => claim.Key, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Count() == 1
                    ? (object)group.First().Value
                    : group.Select(static claim => claim.Value).ToArray(),
                StringComparer.Ordinal);

        return Results.Json(claims);
    }
}
