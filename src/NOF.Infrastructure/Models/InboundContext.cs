using System.ComponentModel;
using System.Reflection;
using NOF.Hosting;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class MessageInboundContext
{
    public object? Message { get; init; }

    public required IServiceProvider Services { get; init; }

    public object? Response { get; set; }

    public required List<Attribute> Attributes { get; init; }

    public required Type HandlerType { get; init; }

    public string? HandlerName => HandlerType.FullName;

    public string? MessageName => Message?.GetType().FullName;
}

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CommandInboundContext : MessageInboundContext;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NotificationInboundContext : MessageInboundContext;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RequestInboundContext : MessageInboundContext
{
    public required MethodInfo MethodInfo { get; init; }

    public required Type ServiceType { get; init; }

    public required string OperationName { get; init; }
}

public sealed class CommandInboundPipelineTypes : MessagePipelineTypes<ICommandInboundMiddleware>;

public sealed class NotificationInboundPipelineTypes : MessagePipelineTypes<INotificationInboundMiddleware>;

public sealed class RequestInboundPipelineTypes : MessagePipelineTypes<IRequestInboundMiddleware>;
