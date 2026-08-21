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
            DeviceAuthorizationEndpoint = $"{issuer}/device_authorization",
            RegistrationEndpoint = $"{issuer}/register",
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
                OAuthGrantTypes.DeviceCode,
                OAuthGrantTypes.TokenExchange
            ],
            TokenEndpointAuthMethodsSupported =
            [
                OAuthClientAuthenticationMethods.ClientSecretBasic,
                OAuthClientAuthenticationMethods.ClientSecretPost,
                OAuthClientAuthenticationMethods.PrivateKeyJwt,
                OAuthClientAuthenticationMethods.None
            ],
            TokenEndpointAuthSigningAlgValuesSupported = [SecurityAlgorithms.RsaSha256],
            RevocationEndpointAuthMethodsSupported =
            [
                OAuthClientAuthenticationMethods.ClientSecretBasic,
                OAuthClientAuthenticationMethods.ClientSecretPost,
                OAuthClientAuthenticationMethods.None
            ],
            IntrospectionEndpointAuthMethodsSupported =
            [
                OAuthClientAuthenticationMethods.ClientSecretBasic,
                OAuthClientAuthenticationMethods.ClientSecretPost
            ],
            SubjectTypesSupported = ["public"],
            IdTokenSigningAlgValuesSupported = [SecurityAlgorithms.RsaSha256],
            CodeChallengeMethodsSupported = ["plain", "S256"],
            ScopesSupported = options.Value.ScopesSupported,
            ClaimsSupported = options.Value.ClaimsSupported
        }));
    }
}
