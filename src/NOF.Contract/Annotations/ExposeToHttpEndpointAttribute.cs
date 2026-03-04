namespace NOF.Contract;

/// <summary>
/// Marks a request type to be exposed as an HTTP endpoint.
/// Requires <see cref="PublicApiAttribute"/> to also be present on the type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class HttpEndpointAttribute : Attribute
{
    /// <summary>
    /// HTTP method
    /// </summary>
    public HttpVerb Method { get; }

    /// <summary>
    /// Route template
    /// </summary>
    public string? Route { get; }

    /// <summary>
    /// Creates a new HttpEndpointAttribute instance
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="route">Route template</param>
    public HttpEndpointAttribute(HttpVerb method, string? route = null)
    {
        Method = method;
        Route = route;
    }
}

/// <summary>
/// Defines the HTTP method for an exposed endpoint.
/// </summary>
public enum HttpVerb
{
    /// <summary>HTTP GET method.</summary>
    Get,
    /// <summary>HTTP POST method.</summary>
    Post,
    /// <summary>HTTP PUT method.</summary>
    Put,
    /// <summary>HTTP DELETE method.</summary>
    Delete,
    /// <summary>HTTP PATCH method.</summary>
    Patch,
}
