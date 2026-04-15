using System.ComponentModel;

namespace NOF.Contract.Extension.Authorization.Jwt;

/// <summary>
/// Service for providing JSON Web Key Set (JWKS) functionality.
/// </summary>
public interface IJwksService : IRpcService
{
    /// <summary>
    /// Gets the JSON Web Key Set (JWKS) document.
    /// </summary>
    /// <returns>The JWKS document.</returns>
    [Summary("Get authority signing keys")]
    [Description("Returns the JSON Web Key Set used by the authority to sign JWT access and refresh tokens.")]
    [Category("JWT Authority")]
    [HttpEndpoint(HttpVerb.Get, JwtAuthorizationEndpoints.Jwks)]
    Result<JwksDocument> GetJwks(GetJwksRequest request);
}
