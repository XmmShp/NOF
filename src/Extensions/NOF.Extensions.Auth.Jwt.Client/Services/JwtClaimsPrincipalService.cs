using NOF;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF;

/// <summary>
/// Service for parsing JWT tokens into ClaimsPrincipal.
/// </summary>
public class JwtClaimsPrincipalService
{
    private readonly JwtValidationService _validationService;

    public JwtClaimsPrincipalService(JwtValidationService validationService)
    {
        _validationService = validationService;
    }

    /// <summary>
    /// Parses a JWT token into ClaimsPrincipal without validation.
    /// </summary>
    /// <param name="token">The JWT token to parse.</param>
    /// <returns>The ClaimsPrincipal containing the token claims.</returns>
    public ClaimsPrincipal ParseToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        if (!tokenHandler.CanReadToken(token))
            throw new ArgumentException("Invalid JWT token", nameof(token));

        var jwtToken = tokenHandler.ReadJwtToken(token);

        // Map JWT claims to NOF claim types
        var claims = jwtToken.Claims.Select(MapJwtClaimToNofClaim);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "JWT"));
    }

    /// <summary>
    /// Parses and validates a JWT token into ClaimsPrincipal.
    /// </summary>
    /// <param name="token">The JWT token to parse and validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ClaimsPrincipal containing the validated token claims, or null if invalid.</returns>
    public async Task<ClaimsPrincipal?> ValidateAndParseTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var claims = await _validationService.ValidateTokenAsync(token, cancellationToken);
        if (claims == null)
            return null;

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);

        // Map JWT claims to NOF claim types
        var mappedClaims = jwtToken.Claims.Select(MapJwtClaimToNofClaim);
        return new ClaimsPrincipal(new ClaimsIdentity(mappedClaims, "JWT"));
    }

    /// <summary>
    /// Maps JWT claim types to NOF claim types for ClaimsPrincipal compatibility.
    /// </summary>
    /// <param name="jwtClaim">The JWT claim to map.</param>
    /// <returns>The mapped claim with NOF claim type.</returns>
    private static Claim MapJwtClaimToNofClaim(Claim jwtClaim)
    {
        // Map JWT standard claim types to NOF claim types
        return jwtClaim.Type switch
        {
            "sub" => new Claim(ClaimTypes.NameIdentifier, jwtClaim.Value),
            "tenant_id" => new Claim(NOFJwtConstants.ClaimTypes.TenantId, jwtClaim.Value),
            "role" => new Claim(ClaimTypes.Role, jwtClaim.Value),
            "permission" => new Claim(NOFJwtConstants.ClaimTypes.Permission, jwtClaim.Value),
            _ => jwtClaim // Keep other claims as-is
        };
    }
}
