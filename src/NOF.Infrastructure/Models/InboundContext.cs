using NOF.Contract;
using NOF.Hosting;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CommandInboundContext
{
    public required object Message { get; init; }

    public required Type HandlerType { get; init; }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NotificationInboundContext
{
    public required object Message { get; init; }

    public required Type HandlerType { get; init; }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RequestInboundContext
{
    public required object Message { get; init; }

    public IResult? Response { get; set; }

    public required Type HandlerType { get; init; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
    public required Type ServiceType { get; init; }

    public required string MethodName { get; init; }
}

public sealed class CommandInboundPipelineTypes
{
    private readonly MessagePipelineTypes<ICommandInboundMiddleware> _inner = new();

    public int Count => _inner.Count;

    public Type this[int index] => _inner[index];

    public void Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, ICommandInboundMiddleware
        => _inner.Add<TMiddleware>();

    public void Freeze() => _inner.Freeze();
}

public sealed class NotificationInboundPipelineTypes
{
    private readonly MessagePipelineTypes<INotificationInboundMiddleware> _inner = new();

    public int Count => _inner.Count;

    public Type this[int index] => _inner[index];

    public void Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, INotificationInboundMiddleware
        => _inner.Add<TMiddleware>();

    public void Freeze() => _inner.Freeze();
}

public sealed class RequestInboundPipelineTypes
{
    private readonly MessagePipelineTypes<IRequestInboundMiddleware> _inner = new();

    public int Count => _inner.Count;

    public Type this[int index] => _inner[index];

    public void Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, IRequestInboundMiddleware
        => _inner.Add<TMiddleware>();

    public void Freeze() => _inner.Freeze();
}
