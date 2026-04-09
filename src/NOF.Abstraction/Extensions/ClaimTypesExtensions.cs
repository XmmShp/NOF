using System.Security.Claims;

namespace NOF.Abstraction;

public static partial class NOFAbstractionExtensions
{
    extension(ClaimTypes)
    {
        /// <summary>
        /// Custom claim type for permissions, separate from standard Role claims.
        /// </summary>
        public static string Permission => NOFAbstractionConstants.Claims.Permission;

        /// <summary>
        /// Well-known claim type for tenant identifier.
        /// </summary>
        public static string TenantId => NOFAbstractionConstants.Claims.TenantId;
    }
}
