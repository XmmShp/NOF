namespace NOF.Application;

/// <summary>
/// Central constants for the NOF Application layer.
/// </summary>
public static class NOFApplicationConstants
{
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

    /// <summary>
    /// Standard transport-level header keys.
    /// </summary>
    public static class Transport
    {
        /// <summary>
        /// Standard HTTP / transport-level header keys used in execution context headers.
        /// </summary>
        public static class Headers
        {
            public const string Authorization = "Authorization";
            public const string TenantId = "NOF.TenantId";
            public const string TraceId = "NOF.Message.TraceId";
            public const string SpanId = "NOF.Message.SpanId";
            public const string MessageId = "NOF.Message.MessageId";
        }
    }
}
