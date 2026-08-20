namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public static class OAuthGrantTypes
{
    public const string AuthorizationCode = "authorization_code";
    public const string ClientCredentials = "client_credentials";
    public const string RefreshToken = "refresh_token";
    public const string TokenExchange = "urn:ietf:params:oauth:grant-type:token-exchange";
}

public static class OAuthClientAuthenticationMethods
{
    public const string ClientSecretBasic = "client_secret_basic";
    public const string ClientSecretPost = "client_secret_post";
    public const string PrivateKeyJwt = "private_key_jwt";
    public const string None = "none";
}

public static class OAuthClientApplicationTypes
{
    public const string Web = "web";
    public const string Native = "native";
}

public static class OAuthSubjectTypes
{
    public const string Public = "public";
}

public static class OAuthSigningAlgorithms
{
    public const string RsaSha256 = "RS256";
}

public static class OAuthClientAssertionTypes
{
    public const string JwtBearer = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
}

public static class OAuthTokenTypes
{
    public const string AccessToken = "urn:ietf:params:oauth:token-type:access_token";
    public const string RefreshToken = "refresh_token";
}

public static class OAuthScope
{
    public const string OpenId = "openid";
    public const string Profile = "profile";
    public const string Email = "email";
    public const string OfflineAccess = "offline_access";
    public const string ClientRegistration = "client.register";
}

public static class OAuthClaimTypes
{
    public const string Actor = "act";
    public const string Subject = "sub";
    public const string ClientId = "client_id";
    public const string Name = "name";
    public const string Email = "email";
    public const string EmailVerified = "email_verified";
    public const string Groups = "groups";
    public const string Entitlements = "entitlements";
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
