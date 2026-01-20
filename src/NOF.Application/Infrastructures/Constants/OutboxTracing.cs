using System.Diagnostics;

namespace NOF;

/// <summary>
/// Outbox 追踪常量
/// </summary>
public static class OutboxTracing
{
    /// <summary>
    /// ActivitySource 名称
    /// </summary>
    public const string ActivitySourceName = "NOF.Outbox";

    /// <summary>
    /// ActivitySource 实例
    /// </summary>
    public static readonly ActivitySource Source = new(ActivitySourceName);

    /// <summary>
    /// Activity 标签名称
    /// </summary>
    public static class Tags
    {
        public const string MessageId = "outbox.message_id";
        public const string MessageType = "outbox.message_type";
        public const string RetryCount = "outbox.retry_count";
        public const string TraceId = "outbox.trace_id";
        public const string SpanId = "outbox.span_id";
    }

    /// <summary>
    /// Activity 名称
    /// </summary>
    public static class ActivityNames
    {
        public const string MessageProcessing = "OutboxMessageProcessing";
    }
}
