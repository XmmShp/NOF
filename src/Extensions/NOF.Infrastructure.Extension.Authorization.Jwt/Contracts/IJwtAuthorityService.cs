using NOF.Contract;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public partial interface IJwtAuthorityService : IRpcService
{
    [HttpEndpoint(HttpVerb.Post, "/auth/jwt/token")]
    Task<Result<GenerateJwtTokenResponse>> GenerateJwtTokenAsync(GenerateJwtTokenRequest request, CancellationToken cancellationToken = default);

    [HttpEndpoint(HttpVerb.Post, "/auth/jwt/refresh/validate")]
    Task<Result<ValidateJwtRefreshTokenResponse>> ValidateJwtRefreshTokenAsync(ValidateJwtRefreshTokenRequest request, CancellationToken cancellationToken = default);

    [HttpEndpoint(HttpVerb.Post, "/auth/jwt/refresh/revoke")]
    Task<Result> RevokeJwtRefreshTokenAsync(RevokeJwtRefreshTokenRequest request, CancellationToken cancellationToken = default);

    [HttpEndpoint(HttpVerb.Get, "/.well-known/jwks.json")]
    Task<Result<GetJwksResponse>> GetJwksAsync(GetJwksRequest request, CancellationToken cancellationToken = default);
}
