namespace NOF.Contract.Extension.Authorization.Jwt;

/// <summary>
/// Well-known HTTP endpoints used by JWT authority and resource server integrations.
/// </summary>
public static class JwtAuthorizationEndpoints
{
    public const string Jwks = "/.well-known/jwks.json";
    public const string Token = "/connect/token";
    public const string Introspect = "/connect/introspect";
    public const string Revocation = "/connect/revocation";
}
