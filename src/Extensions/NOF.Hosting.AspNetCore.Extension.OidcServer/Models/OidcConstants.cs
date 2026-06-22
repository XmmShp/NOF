namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public static class OAuthGrantTypes
{
    public const string AuthorizationCode = "authorization_code";
    public const string ClientCredentials = "client_credentials";
    public const string RefreshToken = "refresh_token";
    public const string TokenExchange = "urn:ietf:params:oauth:grant-type:token-exchange";
}

public static class OAuthTokenTypes
{
    public const string AccessToken = "urn:ietf:params:oauth:token-type:access_token";
}

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

public readonly record struct BearerToken(string Value)
{
    public static bool TryParse(string? value, IFormatProvider? provider, out BearerToken result)
    {
        var token = value?.Trim() ?? string.Empty;
        const string prefix = "Bearer ";
        if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            token = token[prefix.Length..].TrimStart();
        }

        result = new BearerToken(token);
        return !string.IsNullOrWhiteSpace(token);
    }

    public override string ToString() => Value;
}
