using Microsoft.IdentityModel.Tokens;
using NOF.Contract.Extension.Authentication;

namespace NOF.Infrastructure.Extension.Authentication;

/// <summary>
/// In-process JWKS service backed by the current local signing key store.
/// </summary>
public sealed class LocalJwksService(ISigningKeyService signingKeyService) : IJwksService
{
    public async Task<JwksDocument> GetJwksAsync(CancellationToken cancellationToken = default)
    {
        var jwks = (await signingKeyService.GetAllKeysAsync(cancellationToken).ConfigureAwait(false)).Select(ToJwkKeyDocument).ToArray();
        return new JwksDocument { Keys = jwks };
    }

    internal static JwkKeyDocument ToJwkKeyDocument(ManagedSigningKey managedKey)
    {
        var rsa = managedKey.Key.Rsa;
        var parameters = rsa.ExportParameters(false);

        return new JwkKeyDocument
        {
            Kty = "RSA",
            Use = "sig",
            Alg = NOFAuthenticationConstants.Jwt.Algorithm,
            Kid = managedKey.Kid,
            N = Base64UrlEncoder.Encode(parameters.Modulus ?? throw new InvalidOperationException("RSA modulus cannot be null")),
            E = Base64UrlEncoder.Encode(parameters.Exponent ?? throw new InvalidOperationException("RSA exponent cannot be null"))
        };
    }
}
