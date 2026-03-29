namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Request for retrieving the JSON Web Key Set (JWKS).
/// </summary>
public record GetJwksRequest;

/// <summary>
/// Response containing the JWKS document.
/// </summary>
public record GetJwksResponse(JwksDocument Jwks);



