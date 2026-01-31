namespace NOF;

/// <summary>
/// Request for getting JWKS (JSON Web Key Set) for local token validation.
/// </summary>
public record GetJwksRequest : IRequest<GetJwksResponse>;

/// <summary>
/// Response for getting JWKS (JSON Web Key Set).
/// </summary>
public record GetJwksResponse(string? JwksJson);
