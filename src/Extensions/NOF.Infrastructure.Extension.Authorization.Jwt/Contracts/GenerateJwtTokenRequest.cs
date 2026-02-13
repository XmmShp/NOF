using NOF.Contract;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Request for generating JWT token pair.
/// </summary>
public record GenerateJwtTokenRequest(
    string UserId,
    string TenantId,
    string Audience,
    TimeSpan AccessTokenExpiration,
    TimeSpan RefreshTokenExpiration,
    string[]? Permissions = null,
    Dictionary<string, string>? CustomClaims = null
) : IRequest<GenerateJwtTokenResponse>;

/// <summary>
/// Response for generating JWT token pair.
/// </summary>
public record GenerateJwtTokenResponse(TokenPair TokenPair);
