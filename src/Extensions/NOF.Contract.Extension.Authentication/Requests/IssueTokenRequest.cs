namespace NOF.Contract.Extension.Authentication;

/// <summary>
/// Request for generating access tokens.
/// </summary>
public record IssueTokenRequest
{
    public required string Audience { get; set; }
    public TimeSpan AccessTokenExpiration { get; set; }
    public TokenClaim[]? AccessClaims { get; set; }
    public RefreshTokenOptions? RefreshToken { get; set; }
}

/// <summary>
/// Response for generating access tokens.
/// </summary>
public record IssueTokenResponse
{
    public required string AccessToken { get; set; }
    public required DateTime AccessTokenExpiresAtUtc { get; set; }
    public IssuedRefreshToken? RefreshToken { get; set; }
}
