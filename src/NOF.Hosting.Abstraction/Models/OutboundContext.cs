using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using NOF.Contract;

namespace NOF.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RequestOutboundContext
{
    public required object Message { get; init; }

    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public IResult? Response { get; set; }

    public required Type ServiceType { get; init; }

    public required string MethodName { get; init; }
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
