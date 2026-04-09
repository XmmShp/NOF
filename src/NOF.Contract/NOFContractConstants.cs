namespace NOF.Contract;

/// <summary>
/// Central constants for the NOF Framework.
/// </summary>
public static class NOFContractConstants
{
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
