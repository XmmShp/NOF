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
            public const string TenantId = "NOF.TenantId";
            public const string TraceParent = "traceparent";
            public const string MessageId = "NOF.Message.MessageId";
            public const string RpcSuccess = "NOF.Transport.Success";
        }

        public static class Metadatas
        {
            public const string HttpStatusCode = "NOF.Transport.Http.StatusCode";
            public const string HttpHeaderPrefix = "NOF.Transport.Http.Header.";
        }
    }
}
