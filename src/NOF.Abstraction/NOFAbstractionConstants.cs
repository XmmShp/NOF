namespace NOF.Abstraction;

/// <summary>
/// Central constants for the NOF Framework.
/// </summary>
public static class NOFAbstractionConstants
{
    /// <summary>
    /// Tenant-related constants.
    /// </summary>
    public static class Tenant
    {
        /// <summary>
        /// The host tenant ID.
        /// </summary>
        public const string HostId = "host";

        /// <summary>
        /// Normalizes a tenant ID.
        /// </summary>
        public static string NormalizeTenantId(string? tenantId)
            => string.IsNullOrWhiteSpace(tenantId) ? HostId : tenantId;
    }

    public static class Transport
    {
        public static class Headers
        {
            public const string Authorization = "Authorization";
            public const string TenantId = "X-Tenant-Id";
            public const string TraceParent = "traceparent";
            public const string MessageId = "X-Message-Id";
        }
    }
}
