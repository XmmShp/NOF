using System.Security.Claims;

namespace NOF.Application;

public static partial class NOFApplicationExtensions
{
    extension(ClaimTypes)
    {
        /// <summary>
        /// Custom claim type for permissions, separate from standard Role claims.
        /// </summary>
        public static string Permission => "nof.permission";

        /// <summary>
        /// Well-known claim type for tenant identifier.
        /// </summary>
        public static string TenantId => "nof.tenant_id";
    }
}
