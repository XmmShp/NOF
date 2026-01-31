namespace NOF;

/// <summary>
/// Request for revoking all JWT tokens for a user.
/// </summary>
public record RevokeUserJwtTokensRequest(
    string UserId,
    string TenantId) : IRequest<RevokeUserJwtTokensResponse>;

/// <summary>
/// Response for revoking all JWT tokens for a user.
/// </summary>
public record RevokeUserJwtTokensResponse(bool Revoked);
