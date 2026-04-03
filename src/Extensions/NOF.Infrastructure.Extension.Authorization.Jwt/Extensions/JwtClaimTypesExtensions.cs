using System.Security.Claims;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// JWT-specific claim type extensions for <see cref="ClaimTypes"/>.
/// Claim types already defined in the Application layer (e.g., TenantId, Permission) are not duplicated here.
/// </summary>
public static partial class NOFJwtAuthorizationExtensions
{
    extension(ClaimTypes)
    {
        /// <summary>JWT token identifier (jti).</summary>
        public static string JwtId => "jti";
    }
}
