namespace System.Security.Claims;

public static partial class NOFClaimTypesExtensions
{
    extension(ClaimTypes)
    {
        /// <summary>
        /// Standard claim type for entitlements.
        /// </summary>
        public static string Permission => "entitlements";

        /// <summary>
        /// Well-known claim type for tenant identifier.
        /// </summary>
        public static string TenantId => "nof.tenant_id";
    }
}
