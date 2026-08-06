namespace NOF.Contract;

/// <summary>
/// Declares that an RPC service contract can only be invoked in process and must not be exposed by server transports.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class TransportOverMemoryAttribute : TransportOverAttribute;
