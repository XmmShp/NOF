using NOF.Contract;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Request for validating refresh token.
/// </summary>
public record JwtValidateRefreshTokenRequest(string RefreshToken) : IRequest<JwtValidateRefreshTokenResponse>;

/// <summary>
/// Response for validating refresh token.
/// </summary>
public record JwtValidateRefreshTokenResponse(string TokenId, string UserId, string TenantId);
