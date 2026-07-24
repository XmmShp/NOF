using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OidcRoutes = Microsoft.AspNetCore.Routing.NOFOidcServerExtensions;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthIntrospectEndpoint(
    IServiceProvider serviceProvider,
    ITokenService tokenService,
    IOptions<OAuthAuthorizationServerOptions> oauthOptions) : IOAuthIntrospectEndpoint
{
    public async Task<IResult> HandleAsync(OAuthIntrospectEndpointRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return OidcRoutes.CreateOAuthErrorResult("invalid_request", "token is required.");
        }

        var authenticatedClient = await OidcRoutes.AuthenticateClientAsync(
            request.HttpRequest,
            request.ClientId,
            request.ClientSecret,
            string.Empty,
            serviceProvider,
            allowPublicClient: false,
            cancellationToken).ConfigureAwait(false);
        if (authenticatedClient.Error is not null)
        {
            return OidcRoutes.CreateOAuthErrorResult(
                authenticatedClient.Error.Error,
                authenticatedClient.Error.ErrorDescription);
        }

        var result = await tokenService.IntrospectTokenAsync(
            new IntrospectTokenRequest
            {
                Token = request.Token,
                TokenTypeHint = request.TokenTypeHint,
                Audience = oauthOptions.Value.AccessTokenAudience
            },
            cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return OidcRoutes.CreateOAuthErrorResult(
                result.ErrorCode,
                result.Message,
                StatusCodes.Status500InternalServerError);
        }

        return Results.Json(OidcRoutes.BuildIntrospectionResponse(result.Value));
    }
}
