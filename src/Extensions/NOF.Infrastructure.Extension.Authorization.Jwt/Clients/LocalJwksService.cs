using Microsoft.IdentityModel.Tokens;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// In-process JWKS service backed by the current local signing key store.
/// </summary>
public sealed class LocalJwksService(ISigningKeyService signingKeyService) : IJwksService
{
    public async Task<JwksDocument> GetJwksAsync(CancellationToken cancellationToken = default)
    {
        var jwks = (await signingKeyService.GetAllKeysAsync(cancellationToken).ConfigureAwait(false)).Select(ToJsonWebKey).ToArray();
        return new JwksDocument { Keys = jwks };
    }

    internal static JsonWebKey ToJsonWebKey(ManagedSigningKey managedKey)
    {
        var rsa = managedKey.Key.Rsa;
        var parameters = rsa.ExportParameters(false);

        return new JsonWebKey
        {
            Kty = "RSA",
            Use = "sig",
            Alg = NOFJwtAuthorizationConstants.Jwt.Algorithm,
            Kid = managedKey.Kid,
            N = Base64UrlEncoder.Encode(parameters.Modulus ?? throw new InvalidOperationException("RSA modulus cannot be null")),
            E = Base64UrlEncoder.Encode(parameters.Exponent ?? throw new InvalidOperationException("RSA exponent cannot be null"))
        };
    }
}
