using Microsoft.AspNetCore.Http;
using OidcRoutes = Microsoft.AspNetCore.Routing.NOFOidcServerExtensions;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthAuthorizeEndpoint(IServiceProvider serviceProvider) : IOAuthAuthorizeEndpoint
{
    public async Task<IResult> HandleAsync(OAuthAuthorizeEndpointRequest request, CancellationToken cancellationToken)
    {
        var authorizationRequest = new OAuthAuthorizationRequest(
            ResponseType: request.Request.ResponseType,
            ClientId: request.Request.ClientId,
            RedirectUri: request.Request.RedirectUri,
            Scope: OidcRoutes.NormalizeScope(request.Request.Scope),
            State: request.Request.State,
            Nonce: OidcRoutes.EmptyToNull(request.Request.Nonce),
            CodeChallenge: OidcRoutes.EmptyToNull(request.Request.CodeChallenge),
            CodeChallengeMethod: OidcRoutes.EmptyToNull(request.Request.CodeChallengeMethod));

        var validation = await OidcRoutes.ValidateAuthorizationRequestAsync(
            serviceProvider,
            authorizationRequest,
            cancellationToken).ConfigureAwait(false);
        authorizationRequest = validation.Request;
        var validationError = validation.Error;
        if (validationError is not null)
        {
            return OidcRoutes.CreateAuthorizeFailureResult(authorizationRequest, validationError, validation.AllowRedirect);
        }

        return OidcRoutes.CreateAuthorizeFailureResult(
            authorizationRequest,
            OidcRoutes.CreateOAuthError(
                "server_error",
                "OAuth authorize endpoint is not configured. Replace IOAuthAuthorizeEndpoint to implement authorization."),
            allowRedirect: true);
    }
}
