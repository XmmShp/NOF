namespace NOF.Contract.Extension.Authentication;

public sealed record OAuthUserInfoRequest
{
    [FromHeader("Authorization")]
    public BearerToken AccessToken { get; set; }
}
