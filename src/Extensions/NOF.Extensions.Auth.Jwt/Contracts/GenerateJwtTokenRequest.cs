namespace NOF;

/// <summary>
/// Request for generating JWT token pair.
/// </summary>
public record GenerateJwtTokenRequest(
    string UserId,
    string TenantId,
    string Audience,
    TimeSpan AccessTokenExpiration,
    TimeSpan RefreshTokenExpiration,
    string[]? Roles = null,
    string[]? Permissions = null,
    Dictionary<string, string>? CustomClaims = null
) : IRequest<GenerateJwtTokenResponse>;

/// <summary>
/// Response for generating JWT token pair.
/// </summary>
public record GenerateJwtTokenResponse(TokenPair TokenPair);
