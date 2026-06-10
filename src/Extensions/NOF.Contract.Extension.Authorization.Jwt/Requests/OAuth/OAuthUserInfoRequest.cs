using NOF.Contract;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed record OAuthUserInfoRequest
{
    [FromHeader("Authorization")]
    public BearerToken AccessToken { get; set; }
}
