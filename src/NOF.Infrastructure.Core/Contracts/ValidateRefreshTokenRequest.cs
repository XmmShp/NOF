using NOF.Contract;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Request for validating refresh token.
/// </summary>
public record ValidateRefreshTokenRequest(string RefreshToken) : IRequest<ValidateRefreshTokenResponse>;

/// <summary>
/// Response for validating refresh token.
/// </summary>
public record ValidateRefreshTokenResponse(string TokenId, string UserId, string TenantId);
