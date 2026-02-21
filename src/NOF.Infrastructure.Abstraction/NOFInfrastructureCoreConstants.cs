using NOF.Infrastructure.Abstraction;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Central constants for the NOF Infrastructure Core layer.
/// </summary>
public static partial class NOFInfrastructureCoreConstants
{
    /// <summary>
    /// Handler pipeline tracing and metrics constants.
    /// </summary>
    public static class InboundPipeline
    {
        /// <summary>
        /// The ActivitySource name.
        /// </summary>
        public const string ActivitySourceName = "NOF.InboundPipeline";

        /// <summary>
        /// The Meter name.
        /// </summary>
        public const string MeterName = "NOF.InboundPipeline";

        /// <summary>
        /// The ActivitySource instance.
        /// </summary>
        public static readonly ActivitySource Source = new(ActivitySourceName);

        /// <summary>
        /// The Meter instance.
        /// </summary>
        public static readonly Meter Meter = new(MeterName);

        /// <summary>
        /// Activity tag names.
        /// </summary>
        public static class Tags
        {
            public const string HandlerType = "handler.type";
            public const string MessageType = "message.type";
            public const string TenantId = "tenant.id";
        }

        /// <summary>
        /// Metric names.
        /// </summary>
        public static class Metrics
        {
            public const string ExecutionCounter = "nof.handler.executions";
            public const string ExecutionDuration = "nof.handler.duration";
            public const string ErrorCounter = "nof.handler.errors";
        }

        /// <summary>
        /// Metric descriptions.
        /// </summary>
        public static class MetricDescriptions
        {
            public const string ExecutionCounter = "Total number of handler executions";
            public const string ExecutionDuration = "Handler execution duration in milliseconds";
            public const string ErrorCounter = "Total number of handler execution errors";
        }

        /// <summary>
        /// Metric units.
        /// </summary>
        public static class MetricUnits
        {
            public const string Milliseconds = "ms";
        }
    }

    /// <summary>
    /// Standard HTTP / transport-level header keys.
    /// </summary>
    public static partial class Transport
    {
        /// <summary>
        /// Standard HTTP / transport-level header keys used in <see cref="InboundContext.Headers"/>.
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

    /// <summary>
    /// Message tracing constants.
    /// </summary>
    public static class Messaging
    {
        /// <summary>
        /// The ActivitySource name.
        /// </summary>
        public const string ActivitySourceName = "NOF.Messaging";

        /// <summary>
        /// The ActivitySource instance.
        /// </summary>
        public static readonly ActivitySource Source = new(ActivitySourceName);

        /// <summary>
        /// Activity tag names.
        /// </summary>
        public static class Tags
        {
            public const string MessageId = "messaging.message_id";
            public const string MessageType = "messaging.message_type";
            public const string Destination = "messaging.destination";
            public const string TenantId = "messaging.tenant_id";
        }

        /// <summary>
        /// Activity names.
        /// </summary>
        public static class ActivityNames
        {
            public const string MessageSending = "MessageSending";
        }
    }
}
