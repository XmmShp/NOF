using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NOF.Application;
using OidcRoutes = Microsoft.AspNetCore.Routing.NOFOidcServerExtensions;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthTokenEndpoint(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    IOAuthSubjectService subjectService,
    ITokenService tokenService,
    ISigningKeyService signingKeyService,
    IOptions<OAuthAuthorizationServerOptions> oauthOptions) : IOAuthTokenEndpoint
{
    public async Task<IResult> HandleAsync(OAuthTokenEndpointRequest request, CancellationToken cancellationToken)
    {
        var result = request.Request.GrantType switch
        {
            OAuthGrantTypes.AuthorizationCode => await OidcRoutes.TokenFromAuthorizationCodeAsync(
                request.HttpRequest,
                request.Request,
                serviceProvider,
                cacheService,
                subjectService,
                tokenService,
                signingKeyService,
                oauthOptions.Value,
                cancellationToken).ConfigureAwait(false),
            OAuthGrantTypes.ClientCredentials => await OidcRoutes.TokenFromClientCredentialsAsync(
                request.HttpRequest,
                request.Request,
                serviceProvider,
                tokenService,
                oauthOptions.Value,
                cancellationToken).ConfigureAwait(false),
            OAuthGrantTypes.RefreshToken => await OidcRoutes.TokenFromRefreshTokenAsync(
                request.HttpRequest,
                request.Request,
                serviceProvider,
                subjectService,
                tokenService,
                signingKeyService,
                oauthOptions.Value,
                cancellationToken).ConfigureAwait(false),
            OAuthGrantTypes.TokenExchange => await OidcRoutes.TokenFromTokenExchangeAsync(
                request.HttpRequest,
                request.Request,
                serviceProvider,
                subjectService,
                tokenService,
                signingKeyService,
                oauthOptions.Value,
                cancellationToken).ConfigureAwait(false),
            _ => OidcRoutes.Fail(
                "unsupported_grant_type",
                "Only authorization_code, client_credentials, refresh_token, and token-exchange are supported.")
        };

        return result.IsSuccess
            ? Results.Json(result.Value)
            : OidcRoutes.CreateOAuthErrorResult(result.ErrorCode, result.Message);
    }
}
