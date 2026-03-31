namespace NOF.Contract;

/// <summary>
/// Marks a partial class as the HTTP client implementation target for the given RPC service interface.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class HttpServiceClientAttribute<TService> : Attribute
{
}
