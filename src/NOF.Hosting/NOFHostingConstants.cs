using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF.Hosting;

/// <summary>
/// Hosting-related constants used across layers. Placed in Abstraction to avoid cross-layer dependencies.
/// </summary>
public static class NOFHostingConstants
{
    public static class Outbound
    {
        public const string ActivitySourceName = "NOF.Hosting.OutboundPipeline";
        public const string MeterName = "NOF.Hosting.OutboundPipeline";

        public static readonly ActivitySource Source = new(ActivitySourceName);
        public static readonly Meter Meter = new(MeterName);

        public static class Tags
        {
            public const string MessageId = "outbound.message_id";
            public const string MessageType = "outbound.message_type";
            public const string TenantId = "outbound.tenant_id";
        }
    }
}

