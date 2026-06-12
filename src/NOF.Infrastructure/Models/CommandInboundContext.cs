using NOF.Contract;
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
