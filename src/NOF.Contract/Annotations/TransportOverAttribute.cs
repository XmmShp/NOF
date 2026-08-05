namespace NOF.Contract;

/// <summary>
/// Declares the transport used by an RPC service contract.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public abstract class TransportOverAttribute : Attribute;
