using NOF.Hosting.AspNetCore.Extension.OidcServer;
using System.Security.Claims;

namespace NOF.Sample.Services;

public sealed class SampleOAuthAuthorizationHandler : IOAuthAuthorizationHandler
{
    public ValueTask<OAuthAuthorizationResult> AuthorizeAsync(
        OAuthAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out _))
        {
            return ValueTask.FromResult<OAuthAuthorizationResult>(
                new OAuthAuthorizationResult.Failure("invalid_request", "redirect_uri must be an absolute URI."));
        }

        return ValueTask.FromResult<OAuthAuthorizationResult>(
            new OAuthAuthorizationResult.Authorized("demo-user"));
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
                    new(ClaimTypes.NameIdentifier, subject),
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
