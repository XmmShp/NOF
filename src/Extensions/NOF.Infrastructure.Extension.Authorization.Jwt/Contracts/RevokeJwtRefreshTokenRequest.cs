using NOF.Contract;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Request for revoking a JWT refresh token using its TokenId (jti).
/// This is used to revoke refresh tokens. Access tokens are short-lived and don't need revocation.
/// </summary>
public record RevokeJwtRefreshTokenRequest(string TokenId, TimeSpan Expiration) : IRequest;
