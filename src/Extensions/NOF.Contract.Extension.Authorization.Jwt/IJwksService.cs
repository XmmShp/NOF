using Microsoft.IdentityModel.Tokens;
using NOF.Contract;

namespace NOF.Contract.Extension.Authorization.Jwt;

/// <summary>
/// Service for providing JSON Web Key Set (JWKS) functionality.
/// </summary>
public interface IJwksService : IRpcService
{
    /// <summary>
    /// Gets the JSON Web Key Set (JWKS) document.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The JWKS document.</returns>
    [HttpEndpoint(HttpVerb.Get, "/.well-known/jwks.json")]
    Task<JwksDocument> GetJwksAsync(CancellationToken cancellationToken = default);
}
