using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace NOF;

/// <summary>
/// Handler for getting JWKS requests.
/// </summary>
public class GetJwks : IRequestHandler<GetJwksRequest, GetJwksResponse>
{
    private readonly JwtOptions _options;
    private readonly IKeyDerivationService _keyDerivationService;

    public GetJwks(IOptions<JwtOptions> options, IKeyDerivationService keyDerivationService)
    {
        _options = options.Value;
        _keyDerivationService = keyDerivationService;
    }

    public async Task<Result<GetJwksResponse>> HandleAsync(GetJwksRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var keys = await GetJwksAsync(request.Audience);
            return Result.Success(new GetJwksResponse(_options.Issuer, keys));
        }
        catch (Exception ex)
        {
            return Result.Fail(500, ex.Message);
        }
    }

    private Task<JsonWebKey[]> GetJwksAsync(string audience)
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
