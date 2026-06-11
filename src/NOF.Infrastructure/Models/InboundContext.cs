using NOF.Contract;
using NOF.Hosting;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CommandInboundContext : Context
{
    public required MethodInfo MethodInfo { get; init; }

    public required Type HandlerType { get; init; }

    public required Type MessageType { get; init; }

    public IReadOnlyList<object> Metadata { get; init; } = Array.Empty<object>();

    [SetsRequiredMembers]
    private CommandInboundContext(IReadOnlyDictionary<object, object?> items, CommandInboundContext source)
        : base(items)
    {
        MethodInfo = source.MethodInfo;
        HandlerType = source.HandlerType;
        MessageType = source.MessageType;
        Metadata = source.Metadata;
        TenantId = source.TenantId;
    }

    public CommandInboundContext()
    {
    }

    protected override Context Clone(IReadOnlyDictionary<object, object?> items)
        => new CommandInboundContext(items, this);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NotificationInboundContext : Context
{
    public required MethodInfo MethodInfo { get; init; }

    public required Type HandlerType { get; init; }

    public required Type MessageType { get; init; }

    public IReadOnlyList<object> Metadata { get; init; } = Array.Empty<object>();

    [SetsRequiredMembers]
    private NotificationInboundContext(IReadOnlyDictionary<object, object?> items, NotificationInboundContext source)
        : base(items)
    {
        MethodInfo = source.MethodInfo;
        HandlerType = source.HandlerType;
        MessageType = source.MessageType;
        Metadata = source.Metadata;
        TenantId = source.TenantId;
    }

    public NotificationInboundContext()
    {
    }

    protected override Context Clone(IReadOnlyDictionary<object, object?> items)
        => new NotificationInboundContext(items, this);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RequestInboundContext : Context
{
    public IRpcResult? Response { get; set; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
    public required Type ServiceType { get; init; }

    public required MethodInfo ServiceMethodInfo { get; init; }

    public required Type HandlerType { get; init; }

    public required MethodInfo HandlerMethodInfo { get; init; }

    public required Type RequestType { get; init; }

    public required Type ResponseType { get; init; }

    public IReadOnlyList<object> Metadata { get; init; } = Array.Empty<object>();

    [SetsRequiredMembers]
    private RequestInboundContext(IReadOnlyDictionary<object, object?> items, RequestInboundContext source)
        : base(items)
    {
        Response = source.Response;
        ServiceType = source.ServiceType;
        ServiceMethodInfo = source.ServiceMethodInfo;
        HandlerType = source.HandlerType;
        HandlerMethodInfo = source.HandlerMethodInfo;
        RequestType = source.RequestType;
        ResponseType = source.ResponseType;
        Metadata = source.Metadata;
        TenantId = source.TenantId;
    }

    public RequestInboundContext()
    {
    }

    protected override Context Clone(IReadOnlyDictionary<object, object?> items)
        => new RequestInboundContext(items, this);
}
