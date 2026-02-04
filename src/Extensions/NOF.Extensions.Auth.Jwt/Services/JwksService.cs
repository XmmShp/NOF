using Microsoft.IdentityModel.Tokens;

namespace NOF;

/// <summary>
/// Service for providing JSON Web Key Set (JWKS) functionality.
/// </summary>
public interface IJwksService
{
    /// <summary>
    /// Gets the JSON Web Key Set for the specified audience.
    /// </summary>
    /// <param name="audience">The audience identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The JSON Web Key Set containing the public keys.</returns>
    Task<JsonWebKey[]> GetJwksAsync(string audience, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of JWKS service.
/// </summary>
public class JwksService : IJwksService
{
    private readonly IKeyDerivationService _keyDerivationService;

    public JwksService(IKeyDerivationService keyDerivationService)
    {
        _keyDerivationService = keyDerivationService;
    }

    /// <inheritdoc />
    public Task<JsonWebKey[]> GetJwksAsync(string audience, CancellationToken cancellationToken = default)
    {
        // Get cached RSA key for the audience
        var signingKey = _keyDerivationService.GetOrCreateRsaSecurityKey(audience);
        var rsa = signingKey.Rsa;
        var parameters = rsa.ExportParameters(false);

        // Compute key ID using public key DER encoding SHA-256 hash
        var kid = _keyDerivationService.ComputeKeyId(signingKey);

        var keys = new[]
        {
            new JsonWebKey
            {
                Kty = "RSA",
                Use = "sig",
                Alg = NOFJwtConstants.Algorithm,
                Kid = kid,
                N = Base64UrlEncoder.Encode(parameters.Modulus ?? throw new InvalidOperationException("RSA modulus cannot be null")),
                E = Base64UrlEncoder.Encode(parameters.Exponent ?? throw new InvalidOperationException("RSA exponent cannot be null"))
            }
        };

        return Task.FromResult(keys);
    }
}
