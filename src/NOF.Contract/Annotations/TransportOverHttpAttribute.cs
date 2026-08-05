using NOF.Internal;

namespace NOF.Contract;

/// <summary>
/// Declares that an RPC service contract is transported over HTTP.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class TransportOverHttpAttribute : TransportOverAttribute
{
    public TransportOverHttpAttribute(HttpRpcStyle style, string? routePrefix = null)
    {
        if (routePrefix is not null)
        {
            _ = HttpRoutePrefix.Normalize(routePrefix);
        }

        Style = style;
        RoutePrefix = routePrefix;
    }

    public HttpRpcStyle Style { get; }

    public string? RoutePrefix { get; }
}

/// <summary>
/// Selects the HTTP RPC style used by an RPC service contract.
/// </summary>
public enum HttpRpcStyle
{
    JsonRpc,
    ControllerRpc
}
