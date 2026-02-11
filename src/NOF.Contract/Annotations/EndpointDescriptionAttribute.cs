namespace NOF.Contract;

/// <summary>
/// Specifies a description for the endpoint, used to enhance OpenAPI documentation.
/// Unlike System.ComponentModel.DescriptionAttribute, this attribute does not affect the request type's own schema.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class EndpointDescriptionAttribute : Attribute
{
    /// <summary>
    /// Creates a new EndpointDescriptionAttribute instance
    /// </summary>
    /// <param name="description">The description text for the endpoint</param>
    public EndpointDescriptionAttribute(string description)
    {
        Description = description;
    }

    /// <summary>
    /// The description text
    /// </summary>
    public string Description { get; }
}
