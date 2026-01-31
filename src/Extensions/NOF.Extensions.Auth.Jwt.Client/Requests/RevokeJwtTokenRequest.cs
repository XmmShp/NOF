namespace NOF;

/// <summary>
/// Request for revoking JWT token.
/// </summary>
public record RevokeJwtTokenRequest(string TokenId) : IRequest<RevokeJwtTokenResponse>;

/// <summary>
/// Response for revoking JWT token.
/// </summary>
public record RevokeJwtTokenResponse(bool Revoked);
