using NOF.Contract;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Request for validating refresh token.
/// </summary>
public record ValidateJwtRefreshTokenRequest(string RefreshToken) : IRequest<ValidateJwtRefreshTokenResponse>;

/// <summary>
/// Response for validating refresh token.
/// </summary>
public record ValidateJwtRefreshTokenResponse(string TokenId, string UserId, string TenantId);
