using Microsoft.IdentityModel.Tokens;
using NOF.Infrastructure.Core;
using JsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace NOF.Hosting.AspNetCore.Extensions.Authority;

/// <summary>
/// Service for providing JSON Web Key Set (JWKS) functionality.
/// </summary>
public interface IJwksService
{
    /// <summary>
    /// Gets the standard JWKS document containing all active public keys.
    /// </summary>
    /// <returns>A <see cref="Infrastructure.Core.JwksDocument"/> containing all active public keys for token validation.</returns>
    JwksDocument GetJwks();
}

/// <summary>
/// Implementation of JWKS service backed by <see cref="ISigningKeyService"/>.
/// Returns all active keys (current + retired) so clients can validate tokens
/// signed by any key that has not yet been evicted.
/// </summary>
public class JwksService : IJwksService
{
    private readonly ISigningKeyService _signingKeyService;

    public JwksService(ISigningKeyService signingKeyService)
    {
        _signingKeyService = signingKeyService;
    }

    /// <inheritdoc />
    public JwksDocument GetJwks()
    {
        var allKeys = _signingKeyService.AllKeys;

        var jwks = allKeys.Select(managedKey =>
        {
            var rsa = managedKey.Key.Rsa;
            var parameters = rsa.ExportParameters(false);

            return new JsonWebKey
            {
                Kty = "RSA",
                Use = "sig",
                Alg = NOFJwtConstants.Algorithm,
                Kid = managedKey.Kid,
                N = Base64UrlEncoder.Encode(parameters.Modulus ?? throw new InvalidOperationException("RSA modulus cannot be null")),
                E = Base64UrlEncoder.Encode(parameters.Exponent ?? throw new InvalidOperationException("RSA exponent cannot be null"))
            };
        }).ToArray();

        return new JwksDocument { Keys = jwks };
    }
}
