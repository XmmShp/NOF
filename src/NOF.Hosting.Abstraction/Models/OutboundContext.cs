using NOF.Contract;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RequestOutboundContext : Context
{
    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public IRpcResult? Response { get; set; }

    public required Type ServiceType { get; init; }

    public required MethodInfo MethodInfo { get; init; }

    public RequestOutboundContext()
    {
    }

    public RequestOutboundContext(Context context)
        : base(context?.Items ?? throw new ArgumentNullException(nameof(context)))
    {
        TenantId = context.TenantId;
    }

    [SetsRequiredMembers]
    private RequestOutboundContext(IReadOnlyDictionary<object, object?> items, RequestOutboundContext source)
        : base(items)
    {
        Headers = new Dictionary<string, string?>(source.Headers, StringComparer.OrdinalIgnoreCase);
        Response = source.Response;
        ServiceType = source.ServiceType;
        MethodInfo = source.MethodInfo;
        TenantId = source.TenantId;
    }

    protected override Context Clone(IReadOnlyDictionary<object, object?> items)
        => new RequestOutboundContext(items, this);
}
