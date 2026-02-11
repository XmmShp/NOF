using System.Diagnostics;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Message tracing constants.
/// </summary>
public static class MessageTracing
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
