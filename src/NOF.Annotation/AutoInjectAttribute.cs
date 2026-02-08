namespace NOF;

/// <summary>
/// Defines the lifetime of a service registration.
/// </summary>
public enum Lifetime
{
    /// <summary>A single shared instance for the entire application lifetime.</summary>
    Singleton = 0,
    /// <summary>A new instance per scope (e.g., per HTTP request).</summary>
    Scoped = 1,
    /// <summary>A new instance every time the service is requested.</summary>
    Transient = 2
}

/// <summary>
/// Attribute for marking service classes that should be auto-registered in the DI container.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class AutoInjectAttribute : Attribute
{
    /// <summary>
    /// The service lifetime that defines the scope of the service instance.
    /// </summary>
    public Lifetime Lifetime { get; }

    /// <summary>
    /// The service types to register. If null, all implemented interfaces are used.
    /// </summary>
    public Type[]? RegisterTypes { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoInjectAttribute"/> class.
    /// </summary>
    /// <param name="lifetime">The service lifetime.</param>
    public AutoInjectAttribute(Lifetime lifetime)
    {
        Lifetime = lifetime;
    }
}
