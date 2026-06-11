using NOF.Contract;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RequestOutboundContext : Context
{
    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public IRpcResult? Response { get; set; }

    public required Type ServiceType { get; init; }

    public required string MethodName { get; init; }

    public RequestOutboundContext()
    {
    }

    public RequestOutboundContext(Context context)
        : base(context?.Items ?? throw new ArgumentNullException(nameof(context)))
    {
    }

    [SetsRequiredMembers]
    private RequestOutboundContext(IReadOnlyDictionary<object, object?> items, RequestOutboundContext source)
        : base(items)
    {
        Headers = new Dictionary<string, string?>(source.Headers, StringComparer.OrdinalIgnoreCase);
        Response = source.Response;
        ServiceType = source.ServiceType;
        MethodName = source.MethodName;
    }

    protected override Context Clone(IReadOnlyDictionary<object, object?> items)
        => new RequestOutboundContext(items, this);
}

public sealed class RequestOutboundPipelineTypes
{
    private readonly MessagePipelineTypes<IRequestOutboundMiddleware> _inner = new();

    public int Count => _inner.Count;

    public Type this[int index] => _inner[index];

    public void Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, IRequestOutboundMiddleware
        => _inner.Add<TMiddleware>();

    public void Freeze(IServiceProvider services) => _inner.Freeze(services);
}
