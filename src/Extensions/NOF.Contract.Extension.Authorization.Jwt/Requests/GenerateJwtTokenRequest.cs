namespace NOF.Contract.Extension.Authorization.Jwt;

/// <summary>
/// Request for generating JWT token pair.
/// </summary>
public record GenerateJwtTokenRequest
{
    public required string UserId { get; set; }
    public required string TenantId { get; set; }
    public required string Audience { get; set; }
    public TimeSpan AccessTokenExpiration { get; set; }
    public TimeSpan RefreshTokenExpiration { get; set; }
    public string[]? Permissions { get; set; }
    public Dictionary<string, string>? CustomClaims { get; set; }
}

/// <summary>
/// Response for generating JWT token pair.
/// </summary>
public record GenerateJwtTokenResponse
{
    public required TokenPair TokenPair { get; set; }
}
