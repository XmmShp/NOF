namespace NOF;

/// <summary>
/// Request for generating JWT token pair.
/// </summary>
public record GenerateJwtTokenRequest(string TenantId, string UserId) : IRequest<GenerateJwtTokenResponse>
{
    /// <summary>
    /// Gets or sets the list of roles.
    /// </summary>
    public List<string>? Roles { get; init; }

    /// <summary>
    /// Gets or sets the list of permissions.
    /// </summary>
    public List<string>? Permissions { get; init; }

    /// <summary>
    /// Gets or sets additional custom claims.
    /// </summary>
    public Dictionary<string, string>? CustomClaims { get; init; }
}

/// <summary>
/// Response for generating JWT token pair.
/// </summary>
public record GenerateJwtTokenResponse(TokenPair? TokenPair);
