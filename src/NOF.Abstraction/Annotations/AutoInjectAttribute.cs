namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Attribute for marking service classes that should be auto-registered in the DI container.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AutoInjectAttribute : Attribute
{
    /// <summary>
    /// The service lifetime that defines the scope of the service instance.
    /// </summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>
    /// The service types to register. If null, all implemented interfaces are used.
    /// </summary>
    public Type[]? RegisterTypes { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoInjectAttribute"/> class.
    /// </summary>
    /// <param name="lifetime">The service lifetime.</param>
    public AutoInjectAttribute(ServiceLifetime lifetime)
    {
        Lifetime = lifetime;
    }
}
