using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NOF.Infrastructure;
using OidcRoutes = Microsoft.AspNetCore.Routing.NOFOidcServerExtensions;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthRevokeEndpoint(
    IServiceProvider serviceProvider,
    ITokenService tokenService,
    IOptions<OAuthAuthorizationServerOptions> oauthOptions) : IOAuthRevokeEndpoint
{
    public async Task<IResult> HandleAsync(OAuthRevokeEndpointRequest request, CancellationToken cancellationToken)
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
            allowPublicClient: true,
            cancellationToken).ConfigureAwait(false);
        if (authenticatedClient.Error is not null)
        {
            return OidcRoutes.CreateOAuthErrorResult(
                authenticatedClient.Error.Error,
                authenticatedClient.Error.ErrorDescription);
        }

        if (string.Equals(request.TokenTypeHint, OAuthTokenTypes.AccessToken, StringComparison.Ordinal))
        {
            return Results.Ok();
        }

        var validateResult = await tokenService.ValidateRefreshTokenAsync(
            new ValidateRefreshTokenRequest
            {
                RefreshToken = request.Token,
                Audience = oauthOptions.Value.AccessTokenAudience
            },
            cancellationToken).ConfigureAwait(false);
        if (!validateResult.IsSuccess)
        {
            return Results.Ok();
        }

        var refreshClientId = validateResult.Value.Claims
            .FirstOrDefault(static claim => string.Equals(claim.Type, OAuthClaimTypes.ClientId, StringComparison.Ordinal))
            ?.Value;
        if (string.IsNullOrWhiteSpace(refreshClientId)
            || !OidcRoutes.FixedTimeEquals(refreshClientId, authenticatedClient.ClientId))
        {
            return Results.Ok();
        }

        await tokenService.RevokeRefreshTokenAsync(
            new RevokeRefreshTokenRequest
            {
                TokenId = validateResult.Value.TokenId,
                Expiration = oauthOptions.Value.RefreshTokenExpiration
            },
            cancellationToken).ConfigureAwait(false);

        return Results.Ok();
    }
}
