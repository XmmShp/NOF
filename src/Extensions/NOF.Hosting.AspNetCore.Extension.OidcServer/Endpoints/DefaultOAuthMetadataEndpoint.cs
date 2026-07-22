using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthMetadataEndpoint(IOptions<OAuthAuthorizationServerOptions> options) : IOAuthMetadataEndpoint
{
    public ValueTask<IResult> HandleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issuer = options.Value.Issuer.TrimEnd('/');
        return ValueTask.FromResult<IResult>(Results.Json(new OAuthServerMetadata
        {
            Issuer = issuer,
            AuthorizationEndpoint = $"{issuer}/authorize",
            TokenEndpoint = $"{issuer}/token",
            RevocationEndpoint = $"{issuer}/revoke",
            IntrospectionEndpoint = $"{issuer}/introspect",
            UserInfoEndpoint = $"{issuer}/userinfo",
            JwksUri = $"{issuer}/.well-known/jwks.json",
            ResponseTypesSupported = ["code"],
            GrantTypesSupported =
            [
                OAuthGrantTypes.AuthorizationCode,
                OAuthGrantTypes.ClientCredentials,
                OAuthGrantTypes.RefreshToken,
                OAuthGrantTypes.TokenExchange
            ],
            TokenEndpointAuthMethodsSupported = ["client_secret_basic", "client_secret_post", "none"],
            RevocationEndpointAuthMethodsSupported = ["client_secret_basic", "client_secret_post", "none"],
            IntrospectionEndpointAuthMethodsSupported = ["client_secret_basic", "client_secret_post"],
            SubjectTypesSupported = ["public"],
            IdTokenSigningAlgValuesSupported = [SecurityAlgorithms.RsaSha256],
            CodeChallengeMethodsSupported = ["plain", "S256"],
            ScopesSupported = options.Value.ScopesSupported,
            ClaimsSupported = options.Value.ClaimsSupported
        }));
    }
}
