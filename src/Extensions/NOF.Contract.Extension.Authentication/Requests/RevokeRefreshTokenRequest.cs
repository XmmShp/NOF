namespace NOF.Contract.Extension.Authentication;

/// <summary>
/// Request for revoking a JWT refresh token using its TokenId (jti).
/// This is used to revoke refresh tokens. Access tokens are short-lived and don't need revocation.
/// </summary>
public record RevokeRefreshTokenRequest
{
    public required string TokenId { get; set; }
    public TimeSpan Expiration { get; set; }
}
