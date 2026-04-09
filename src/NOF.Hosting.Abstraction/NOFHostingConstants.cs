using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF.Hosting;

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

    public static class Tenant
    {
        public const string HostId = "";

        public static string NormalizeTenantId(string? tenantId)
            => string.IsNullOrWhiteSpace(tenantId) ? HostId : tenantId;
    }

    public static class Transport
    {
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
