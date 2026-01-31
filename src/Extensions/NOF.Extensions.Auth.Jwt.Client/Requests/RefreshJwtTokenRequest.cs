namespace NOF;

/// <summary>
/// Request for refreshing JWT token.
/// </summary>
public record RefreshJwtTokenRequest(
    string RefreshToken) : IRequest<RefreshJwtTokenResponse>;

/// <summary>
/// Response for refreshing JWT token.
/// </summary>
public record RefreshJwtTokenResponse(TokenPair? TokenPair);
