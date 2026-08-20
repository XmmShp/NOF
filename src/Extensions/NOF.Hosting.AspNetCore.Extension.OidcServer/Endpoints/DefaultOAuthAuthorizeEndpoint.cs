using Microsoft.AspNetCore.Http;
using OidcRoutes = Microsoft.AspNetCore.Routing.NOFOidcServerExtensions;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthAuthorizeEndpoint : IOAuthAuthorizeEndpoint
{
    public Task<IResult> HandleAsync(OAuthAuthorizeEndpointRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var authorizationRequest = new OAuthAuthorizationRequest(
            ResponseType: request.Request.ResponseType,
            ClientId: request.Request.ClientId,
            RedirectUri: request.Request.RedirectUri,
            Scope: OidcRoutes.NormalizeScope(request.Request.Scope),
            State: request.Request.State,
            Nonce: OidcRoutes.EmptyToNull(request.Request.Nonce),
            CodeChallenge: OidcRoutes.EmptyToNull(request.Request.CodeChallenge),
            CodeChallengeMethod: OidcRoutes.EmptyToNull(request.Request.CodeChallengeMethod));

        return Task.FromResult<IResult>(OidcRoutes.CreateAuthorizeFailureResult(
            authorizationRequest,
            OidcRoutes.CreateOAuthError(
                "server_error",
                "OAuth authorize endpoint is not configured. Replace IOAuthAuthorizeEndpoint to implement authorization."),
            allowRedirect: true));
    }
}
