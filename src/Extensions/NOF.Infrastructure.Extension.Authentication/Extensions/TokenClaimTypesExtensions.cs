using System.Security.Claims;

namespace NOF.Infrastructure.Extension.Authentication;

/// <summary>
/// JWT-specific claim type extensions for <see cref="ClaimTypes"/>.
/// Claim types already defined in the Application layer (e.g., TenantId, Permission) are not duplicated here.
/// </summary>
public static partial class NOFAuthenticationExtensions
{
    extension(ClaimTypes)
    {
        /// <summary>access token identifier (jti).</summary>
        public static string JwtId => "jti";
    }
}
