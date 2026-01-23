using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF;

/// <summary>
/// Handler 管道追踪和指标常量
/// </summary>
public static class HandlerPipelineTracing
{
    /// <summary>
    /// ActivitySource 名称
    /// </summary>
    public const string ActivitySourceName = "NOF.HandlerPipeline";

    /// <summary>
    /// Meter 名称
    /// </summary>
    public const string MeterName = "NOF.HandlerPipeline";

    /// <summary>
    /// ActivitySource 实例
    /// </summary>
    public static readonly ActivitySource Source = new(ActivitySourceName);

    /// <summary>
    /// Meter 实例
    /// </summary>
    public static readonly Meter Meter = new(MeterName);

    /// <summary>
    /// Activity 标签名称
    /// </summary>
    public static class Tags
    {
        public const string HandlerType = "handler.type";
        public const string MessageType = "message.type";
        public const string TenantId = "tenant.id";
    }

    /// <summary>
    /// 指标名称
    /// </summary>
    public static class Metrics
    {
        public const string ExecutionCounter = "nof.handler.executions";
        public const string ExecutionDuration = "nof.handler.duration";
        public const string ErrorCounter = "nof.handler.errors";
    }

    /// <summary>
    /// 指标描述
    /// </summary>
    public static class MetricDescriptions
    {
        public const string ExecutionCounter = "Total number of handler executions";
        public const string ExecutionDuration = "Handler execution duration in milliseconds";
        public const string ErrorCounter = "Total number of handler execution errors";
    }

    /// <summary>
    /// 指标单位
    /// </summary>
    public static class MetricUnits
    {
        public const string Milliseconds = "ms";
    }
}
