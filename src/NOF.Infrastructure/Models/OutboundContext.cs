using NOF.Hosting;
using NOF.Contract;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CommandOutboundContext : Context
{
    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public CommandOutboundContext()
    {
    }

    public CommandOutboundContext(Context context)
        : base(context?.Items ?? throw new ArgumentNullException(nameof(context)))
    {
    }

    private CommandOutboundContext(IReadOnlyDictionary<object, object?> items, CommandOutboundContext source)
        : base(items)
    {
        Headers = new Dictionary<string, string?>(source.Headers, StringComparer.OrdinalIgnoreCase);
    }

    protected override Context Clone(IReadOnlyDictionary<object, object?> items)
        => new CommandOutboundContext(items, this);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NotificationOutboundContext : Context
{
    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public NotificationOutboundContext()
    {
    }

    public NotificationOutboundContext(Context context)
        : base(context?.Items ?? throw new ArgumentNullException(nameof(context)))
    {
    }

    private NotificationOutboundContext(IReadOnlyDictionary<object, object?> items, NotificationOutboundContext source)
        : base(items)
    {
        Headers = new Dictionary<string, string?>(source.Headers, StringComparer.OrdinalIgnoreCase);
    }

    protected override Context Clone(IReadOnlyDictionary<object, object?> items)
        => new NotificationOutboundContext(items, this);
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
