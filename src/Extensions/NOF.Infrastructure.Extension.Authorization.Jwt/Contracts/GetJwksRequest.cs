using NOF.Contract;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Request for retrieving the JSON Web Key Set (JWKS).
/// </summary>
[PublicApi]
[HttpEndpoint(HttpVerb.Get, "/.well-known/jwks.json")]
public record GetJwksRequest : IRequest<GetJwksResponse>;

/// <summary>
/// Response containing the JWKS document.
/// </summary>
public record GetJwksResponse(JwksDocument Jwks);
