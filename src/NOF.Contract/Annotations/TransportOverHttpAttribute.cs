namespace NOF.Contract;

/// <summary>
/// Declares that an RPC service contract is transported over HTTP.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class TransportOverHttpAttribute(HttpRpcStyle style, string? routePrefix = null) : TransportOverAttribute
{
    public HttpRpcStyle Style { get; } = style;

    public string? RoutePrefix { get; } = routePrefix;
}

/// <summary>
/// Selects the HTTP RPC style used by an RPC service contract.
/// </summary>
public enum HttpRpcStyle
{
    JsonRpc,
    ControllerRpc
}
