namespace NOF.Contract.Extension.Authorization.Jwt;

/// <summary>
/// Request for generating JWT tokens.
/// </summary>
public record GenerateJwtTokenRequest
{
    public required string Audience { get; set; }
    public TimeSpan AccessTokenExpiration { get; set; }
    public KeyValuePair<string, string>[]? AccessClaims { get; set; }
    public JwtRefreshTokenOptions? RefreshToken { get; set; }
}

/// <summary>
/// Response for generating JWT tokens.
/// </summary>
public record GenerateJwtTokenResponse
{
    public required string AccessToken { get; set; }
    public required DateTime AccessTokenExpiresAtUtc { get; set; }
    public IssuedJwtRefreshToken? RefreshToken { get; set; }
}
