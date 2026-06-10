namespace NOF.Contract.Extension.Authentication;

public static class OAuthScope
{
    public const string OpenId = "openid";
    public const string Profile = "profile";
    public const string Email = "email";
}

public static class OAuthClaimTypes
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Email = "email";
    public const string Groups = "groups";
    public const string Nonce = "nonce";
    public const string IssuedAt = "iat";
    public const string Scope = "scope";
    public const string SessionId = "sid";
}
