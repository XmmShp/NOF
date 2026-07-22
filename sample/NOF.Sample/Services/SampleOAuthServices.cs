using Microsoft.AspNetCore.Http;
using NOF.Hosting.AspNetCore.Extension.OidcServer;
using System.Security.Claims;

namespace NOF.Sample.Services;

public sealed class SampleOAuthAuthorizeEndpoint(OAuthAuthorizationCodeIssuer authorizationCodeIssuer) : IOAuthAuthorizeEndpoint
{
    public async Task<IResult> HandleAsync(
        OAuthAuthorizeEndpointRequest request,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.Request.RedirectUri, UriKind.Absolute, out _))
        {
            return Results.BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "redirect_uri must be an absolute URI."
            });
        }

        var authorizationRequest = new OAuthAuthorizationRequest(
            ResponseType: request.Request.ResponseType,
            ClientId: request.Request.ClientId,
            RedirectUri: request.Request.RedirectUri,
            Scope: request.Request.Scope,
            State: request.Request.State,
            Nonce: request.Request.Nonce,
            CodeChallenge: request.Request.CodeChallenge,
            CodeChallengeMethod: request.Request.CodeChallengeMethod);

        return Results.Redirect(
            await authorizationCodeIssuer.CreateRedirectUriAsync(
                authorizationRequest,
                "demo-user",
                request.WasRedirectUriSupplied,
                cancellationToken).ConfigureAwait(false));
    }
}

public sealed class SampleOAuthSubjectService : IOAuthSubjectService
{
    public ValueTask<OAuthSubjectProfile?> GetProfileAsync(
        string subject,
        IReadOnlySet<string> scopes,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(subject, "demo-user", StringComparison.Ordinal))
        {
            return ValueTask.FromResult<OAuthSubjectProfile?>(null);
        }

        return ValueTask.FromResult<OAuthSubjectProfile?>(
            OAuthSubjectProfile.Create(
                subject,
                accessTokenClaims:
                [
                    new(ClaimTypes.TenantId, "sample-tenant")
                ],
                identityClaims:
                [
                    new(OAuthClaimTypes.Name, "NOF Sample User"),
                    new(OAuthClaimTypes.Email, "sample.user@nof.local"),
                    new(OAuthClaimTypes.Groups, "admins")
                ]));
    }
}
