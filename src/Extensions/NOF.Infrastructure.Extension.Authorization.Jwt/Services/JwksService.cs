using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;
using NOF.Contract.Extension.Authorization.Jwt;
using JsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Implementation of JWKS service backed by <see cref="ISigningKeyService"/>.
/// Returns all active keys (current + retired) so clients can validate tokens
/// signed by any key that has not yet been evicted.
/// </summary>
public partial class JwksService : RpcServer<IJwksService>;

public sealed class GetJwksHandler : JwksService.GetJwks
{
    private readonly ISigningKeyService _signingKeyService;

    public GetJwksHandler(ISigningKeyService signingKeyService)
    {
        _signingKeyService = signingKeyService;
    }

    /// <inheritdoc />
    public override Task<Result<JwksDocument>> HandleAsync(GetJwksRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        var allKeys = _signingKeyService.AllKeys;

        var jwks = allKeys.Select(managedKey =>
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
        }).ToArray();

        return Task.FromResult(Result.Success(new JwksDocument { Keys = jwks }));
    }
}
