using System.ComponentModel;

namespace NOF.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class MessageOutboundContext
{
    public object? Message { get; init; }

    public required IServiceProvider Services { get; init; }

    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public object? Response { get; set; }

    public string? MessageName => Message?.GetType().FullName;
}

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CommandOutboundContext : MessageOutboundContext;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NotificationOutboundContext : MessageOutboundContext;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RequestOutboundContext : MessageOutboundContext
{
    public required Type ServiceType { get; init; }

    public required string OperationName { get; init; }
}

public sealed class CommandOutboundPipelineTypes : MessagePipelineTypes<ICommandOutboundMiddleware>;

public sealed class NotificationOutboundPipelineTypes : MessagePipelineTypes<INotificationOutboundMiddleware>;

public sealed class RequestOutboundPipelineTypes : MessagePipelineTypes<IRequestOutboundMiddleware>;
