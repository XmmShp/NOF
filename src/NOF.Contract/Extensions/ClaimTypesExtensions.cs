using System.Security.Claims;

namespace NOF.Contract;

public static partial class NOFContractExtensions
{
    extension(ClaimTypes)
    {
        /// <summary>
        /// Custom claim type for permissions, separate from standard Role claims.
        /// </summary>
        public static string Permission => IUserContext.PermissionClaimType;

        /// <summary>
        /// Well-known claim type for tenant identifier.
        /// </summary>
        public static string TenantId => "nof.tenant_id";
    }
}
