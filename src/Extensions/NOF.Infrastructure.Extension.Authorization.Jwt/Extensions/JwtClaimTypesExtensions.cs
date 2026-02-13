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

        /// <summary>Subject (sub).</summary>
        public static string Subject => "sub";

        /// <summary>Issuer (iss).</summary>
        public static string Issuer => "iss";

        /// <summary>Audience (aud).</summary>
        public static string Audience => "aud";

        /// <summary>Issued at (iat).</summary>
        public static string IssuedAt => "iat";

        /// <summary>Expires at (exp).</summary>
        public static string ExpiresAt => "exp";
    }
}
