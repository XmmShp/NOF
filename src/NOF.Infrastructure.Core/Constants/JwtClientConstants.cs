namespace NOF;

/// <summary>
/// Constants for the JWT client.
/// </summary>
public static class JwtClientConstants
{
    /// <summary>
    /// The named HTTP client used for fetching JWKS from the authority.
    /// </summary>
    public const string JwksHttpClientName = "NOF.JwtClient.Jwks";

    /// <summary>
    /// The well-known JWKS endpoint path.
    /// </summary>
    public const string JwksEndpointPath = "/.well-known/jwks.json";
}
