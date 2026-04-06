namespace NOF.Application;

/// <summary>
/// Marks a service implementation class as the container for generated
/// one-method nested interfaces derived from an <see cref="Contract.IRpcService"/> interface.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ServiceImplementationAttribute<TService> : Attribute
    where TService : Contract.IRpcService;
