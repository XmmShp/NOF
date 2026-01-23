using System.Diagnostics;

namespace NOF;

/// <summary>
/// 消息追踪常量
/// </summary>
public static class MessageTracing
{
    /// <summary>
    /// ActivitySource 名称
    /// </summary>
    public const string ActivitySourceName = "NOF.Messaging";

    /// <summary>
    /// ActivitySource 实例
    /// </summary>
    public static readonly ActivitySource Source = new(ActivitySourceName);

    /// <summary>
    /// Activity 标签名称
    /// </summary>
    public static class Tags
    {
        public const string MessageId = "messaging.message_id";
        public const string MessageType = "messaging.message_type";
        public const string Destination = "messaging.destination";
        public const string TenantId = "messaging.tenant_id";
    }

    /// <summary>
    /// Activity 名称
    /// </summary>
    public static class ActivityNames
    {
        public const string MessageSending = "MessageSending";
    }
}
