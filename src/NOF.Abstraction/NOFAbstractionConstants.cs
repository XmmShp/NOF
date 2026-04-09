namespace NOF.Abstraction;

/// <summary>
/// Central constants for the NOF Framework.
/// </summary>
public static class NOFAbstractionConstants
{
    /// <summary>
    /// Well-known claim type names used by NOF.
    /// </summary>
    public static class Claims
    {
        /// <summary>
        /// Custom claim type for permissions, separate from standard Role claims.
        /// </summary>
        public const string Permission = "nof.permission";

        /// <summary>
        /// Well-known claim type for tenant identifier.
        /// </summary>
        public const string TenantId = "nof.tenant_id";
    }

    /// <summary>
    /// Tenant-related constants.
    /// </summary>
    public static class Tenant
    {
        /// <summary>
        /// The host tenant ID.
        /// </summary>
        public const string HostId = "";

        /// <summary>
        /// Normalizes a tenant ID.
        /// </summary>
        public static string NormalizeTenantId(string? tenantId)
            => string.IsNullOrWhiteSpace(tenantId) ? HostId : tenantId;
    }
}
