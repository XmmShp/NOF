namespace NOF;

/// <summary>
/// Specifies a custom endpoint name for a message type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class EndpointNameAttribute : Attribute
{
    /// <summary>Initializes a new instance with the specified endpoint name.</summary>
    /// <param name="name">The endpoint name.</param>
    public EndpointNameAttribute(string name)
    {
        Name = name;
    }

    /// <summary>Gets the endpoint name.</summary>
    public string Name { get; }
}
