using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace NOF;

/// <summary>
/// Service for providing JSON Web Key Set (JWKS) functionality.
/// </summary>
public interface IJwksService
{
    /// <summary>
    /// Gets the JSON Web Key Set response for the specified audience.
    /// </summary>
    /// <param name="audience">The audience identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The JWKS response containing issuer and public keys.</returns>
    Task<GetJwksResponse> GetJwksAsync(string audience, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of JWKS service.
/// </summary>
public class JwksService : IJwksService
{
    private readonly JwtOptions _options;
    private readonly IKeyDerivationService _keyDerivationService;

    public JwksService(IOptions<JwtOptions> options, IKeyDerivationService keyDerivationService)
    {
        _options = options.Value;
        _keyDerivationService = keyDerivationService;
    }

    /// <inheritdoc />
    public Task<GetJwksResponse> GetJwksAsync(string audience, CancellationToken cancellationToken = default)
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

        var response = new GetJwksResponse(_options.Issuer, keys);

        return Task.FromResult(response);
    }
}
