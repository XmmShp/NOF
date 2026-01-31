using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace NOF;

/// <summary>
/// Handler for getting JWKS requests.
/// </summary>
public class GetJwks : IRequestHandler<GetJwksRequest, GetJwksResponse>
{
    private readonly JwtOptions _options;
    private readonly RsaSecurityKey _signingKey;

    public GetJwks(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _signingKey = CreateRsaSecurityKey(_options.SecurityKey);
    }

    public async Task<Result<GetJwksResponse>> HandleAsync(GetJwksRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var jwks = await GetJwksAsync();
            return Result.Success(new GetJwksResponse(jwks));
        }
        catch (Exception ex)
        {
            return Result.Fail(500, ex.Message);
        }
    }

    private async Task<string> GetJwksAsync()
    {
        var rsa = _signingKey.Rsa;
        var parameters = rsa.ExportParameters(false);

        var jwks = new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = _options.Algorithm,
                    kid = Guid.NewGuid().ToString(),
                    n = Base64Url.Encode(parameters.Modulus),
                    e = Base64Url.Encode(parameters.Exponent)
                }
            }
        };

        return JsonSerializer.Serialize(jwks, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private RsaSecurityKey CreateRsaSecurityKey(string keyString)
    {
        var rsa = RSA.Create();

        if (!string.IsNullOrEmpty(keyString) && keyString.Length > 100)
        {
            try
            {
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(keyString), out _);
            }
            catch
            {
                rsa = RSA.Create(2048);
            }
        }
        else
        {
            rsa = RSA.Create(2048);
        }

        return new RsaSecurityKey(rsa);
    }
}
