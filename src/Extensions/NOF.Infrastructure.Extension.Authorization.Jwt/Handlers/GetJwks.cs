using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Handler for <see cref="GetJwksRequest"/> that retrieves the JWKS from the registered <see cref="IJwksProvider"/>.
/// </summary>
public class GetJwks : IRequestHandler<GetJwksRequest, GetJwksResponse>
{
    private readonly IJwksProvider _jwksProvider;

    public GetJwks(IJwksProvider jwksProvider)
    {
        _jwksProvider = jwksProvider;
    }

    public async Task<Result<GetJwksResponse>> HandleAsync(GetJwksRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var keys = await _jwksProvider.GetSecurityKeysAsync(cancellationToken);

            var jwks = keys.Select(key =>
            {
                if (key is RsaSecurityKey rsaKey)
                {
                    var parameters = rsaKey.Rsa?.ExportParameters(false)
                        ?? rsaKey.Parameters;

                    return new JsonWebKey
                    {
                        Kty = "RSA",
                        Use = "sig",
                        Alg = NOFJwtAuthorizationConstants.Jwt.Algorithm,
                        Kid = rsaKey.KeyId,
                        N = Base64UrlEncoder.Encode(parameters.Modulus!),
                        E = Base64UrlEncoder.Encode(parameters.Exponent!)
                    };
                }

                return null;
            }).Where(k => k is not null).ToArray();

            return Result.Success(new GetJwksResponse(new JwksDocument { Keys = jwks! }));
        }
        catch (Exception ex)
        {
            return Result.Fail(500, $"Failed to retrieve JWKS: {ex.Message}");
        }
    }
}
