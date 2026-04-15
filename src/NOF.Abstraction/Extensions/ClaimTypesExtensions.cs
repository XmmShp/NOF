namespace System.Security.Claims;

public static partial class NOFClaimTypesExtensions
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
