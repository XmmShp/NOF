using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CommandOutboundContext
{
    public required object Message { get; init; }

    public required IServiceProvider Services { get; init; }

    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NotificationOutboundContext
{
    public required object Message { get; init; }

    public required IServiceProvider Services { get; init; }

    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RequestOutboundContext
{
    public required object Message { get; init; }

    public required IServiceProvider Services { get; init; }

    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public object? Response { get; set; }

    public required Type ServiceType { get; init; }

    public required string MethodName { get; init; }
}

public sealed class CommandOutboundPipelineTypes
{
    private readonly MessagePipelineTypes<ICommandOutboundMiddleware> _inner = new();

    public int Count => _inner.Count;

    public Type this[int index] => _inner[index];

    public void Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, ICommandOutboundMiddleware
        => _inner.Add<TMiddleware>();

    public void Freeze() => _inner.Freeze();
}

public sealed class NotificationOutboundPipelineTypes
{
    private readonly MessagePipelineTypes<INotificationOutboundMiddleware> _inner = new();

    public int Count => _inner.Count;

    public Type this[int index] => _inner[index];

    public void Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, INotificationOutboundMiddleware
        => _inner.Add<TMiddleware>();

    public void Freeze() => _inner.Freeze();
}

public sealed class RequestOutboundPipelineTypes
{
    private readonly MessagePipelineTypes<IRequestOutboundMiddleware> _inner = new();

    public int Count => _inner.Count;

    public Type this[int index] => _inner[index];

    public void Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, IRequestOutboundMiddleware
        => _inner.Add<TMiddleware>();

    public void Freeze() => _inner.Freeze();
}
