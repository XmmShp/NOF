namespace NOF.Contract.Extension.Authorization.Jwt;

/// <summary>
/// Request for validating refresh token.
/// </summary>
public record ValidateJwtRefreshTokenRequest
{
    public required string RefreshToken { get; set; }
}

/// <summary>
/// Response for validating refresh token.
/// </summary>
public record ValidateJwtRefreshTokenResponse
{
    public required string TokenId { get; set; }
    public required string UserId { get; set; }
    public required string TenantId { get; set; }
}
