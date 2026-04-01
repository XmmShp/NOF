namespace NOF.Contract.Extension.Authorization.Jwt;

/// <summary>
/// Request for validating refresh token.
/// </summary>
public record ValidateJwtRefreshTokenRequest(string RefreshToken);

/// <summary>
/// Response for validating refresh token.
/// </summary>
public record ValidateJwtRefreshTokenResponse(string TokenId, string UserId, string TenantId);
