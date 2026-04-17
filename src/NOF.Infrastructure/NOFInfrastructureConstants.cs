using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF.Infrastructure;

/// <summary>
/// Central constants for the NOF Infrastructure Core layer.
/// </summary>
public static class NOFInfrastructureConstants
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
    /// Outbound pipeline tracing and metrics constants.
    /// </summary>
    public static class OutboundPipeline
    {
        /// <summary>
        /// The ActivitySource name.
        /// </summary>
        public const string ActivitySourceName = "NOF.OutboundPipeline";

        /// <summary>
        /// The Meter name.
        /// </summary>
        public const string MeterName = "NOF.OutboundPipeline";

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
            public const string MessageId = "outbound.message_id";
            public const string MessageType = "outbound.message_type";
            public const string TenantId = "outbound.tenant_id";
        }

        /// <summary>
        /// Metric names.
        /// </summary>
        public static class Metrics
        {
            public const string ExecutionCounter = "nof.outbound.executions";
            public const string ExecutionDuration = "nof.outbound.duration";
            public const string ErrorCounter = "nof.outbound.errors";
        }

        /// <summary>
        /// Metric descriptions.
        /// </summary>
        public static class MetricDescriptions
        {
            public const string ExecutionCounter = "Total number of outbound dispatches";
            public const string ExecutionDuration = "Outbound dispatch duration in milliseconds";
            public const string ErrorCounter = "Total number of outbound dispatch errors";
        }

        /// <summary>
        /// Metric units.
        /// </summary>
        public static class MetricUnits
        {
            public const string Milliseconds = "ms";
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
