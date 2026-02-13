namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Constants for the NOF JWT authorization extension.
/// </summary>
public static class NOFJwtAuthorizationConstants
{
    /// <summary>
    /// Constants for JWT authentication.
    /// </summary>
    public static class Jwt
    {
        /// <summary>
        /// Default algorithm for JWT signing.
        /// </summary>
        public const string Algorithm = "RS256";
    }

    /// <summary>
    /// Constants for the JWT client.
    /// </summary>
    public static class JwtClient
    {
        /// <summary>
        /// The named HTTP client used for fetching JWKS from the authority.
        /// </summary>
        public const string JwksHttpClientName = "NOF.JwtClient.Jwks";
    }
}
