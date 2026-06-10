namespace NOF.Contract.Extension.Authentication;

/// <summary>
/// Request for validating refresh token.
/// </summary>
public record ValidateRefreshTokenRequest
{
    public required string RefreshToken { get; set; }
}

/// <summary>
/// Response for validating refresh token.
/// </summary>
public record ValidateRefreshTokenResponse
{
    public required string TokenId { get; set; }
    public required TokenClaim[] Claims { get; set; }
}
