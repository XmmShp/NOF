using NOF;

namespace NOF;

/// <summary>
/// Client service for JWT token validation using NOF RequestSender.
/// </summary>
public class JwtClientService
{
    private readonly IRequestSender _requestSender;
    private readonly JwtValidationService _validationService;

    public JwtClientService(IRequestSender requestSender, JwtValidationService validationService)
    {
        _requestSender = requestSender;
        _validationService = validationService;
    }

    /// <summary>
    /// Validates a JWT token locally.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The token claims if valid, null otherwise.</returns>
    public async Task<JwtClaims?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _validationService.ValidateTokenAsync(token, cancellationToken);
    }

    /// <summary>
    /// Gets the JWKS for local token validation.
    /// </summary>
    /// <param name="audience">The audience for which to get the JWKS.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The JWKS keys array, or null if failed.</returns>
    public async Task<JsonWebKey[]?> GetJwksAsync(string audience, CancellationToken cancellationToken = default)
    {
        var result = await _requestSender.SendAsync(new GetJwksRequest(audience), cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            // Handle error
            return null;
        }

        return result.Value?.Keys;
    }
}
