using NOF.Contract;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NotificationInboundContext : Context
{
    public required MethodInfo MethodInfo { get; init; }

    public required Type HandlerType { get; init; }

    public required Type MessageType { get; init; }

    [SetsRequiredMembers]
    private NotificationInboundContext(IReadOnlyDictionary<object, object?> items, NotificationInboundContext source)
        : base(items)
    {
        MethodInfo = source.MethodInfo;
        HandlerType = source.HandlerType;
        MessageType = source.MessageType;
    }

    public NotificationInboundContext()
    {
    }

    protected override Context Clone(IReadOnlyDictionary<object, object?> items)
        => new NotificationInboundContext(items, this);
}
