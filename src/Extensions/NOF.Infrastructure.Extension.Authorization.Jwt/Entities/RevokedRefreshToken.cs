using NOF.Infrastructure;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

[HostOnly]
public sealed class RevokedRefreshToken
{
    public string TokenId { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
}
