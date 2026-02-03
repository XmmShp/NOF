using Microsoft.Extensions.Options;

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
        // Derive client-specific key from master key based on audience
        var clientKey = _keyDerivationService.DeriveClientKey(audience);
        var signingKey = _keyDerivationService.CreateRsaSecurityKey(clientKey);
        var rsa = signingKey.Rsa;
        var parameters = rsa.ExportParameters(false);

        var keys = new[]
        {
            new JsonWebKey
            {
                Kty = "RSA",
                Use = "sig",
                Alg = NOFJwtConstants.Algorithm,
                Kid = clientKey[^16..], // Use last 16 characters of client key as key ID
                N = Convert.ToBase64String(parameters.Modulus ?? throw new InvalidOperationException("RSA modulus cannot be null")),
                E = Convert.ToBase64String(parameters.Exponent ?? throw new InvalidOperationException("RSA exponent cannot be null"))
            }
        };

        return Task.FromResult(keys);
    }
}
