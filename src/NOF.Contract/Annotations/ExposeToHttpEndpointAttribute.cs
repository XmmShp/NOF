namespace NOF;

/// <summary>
/// Marks a request type to be exposed as an HTTP endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class ExposeToHttpEndpointAttribute : Attribute
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
    /// Operation name for generating client method names. If null, uses the request type name (without "Request" suffix)
    /// </summary>
    public string? OperationName { get; init; }

    /// <summary>
    /// Creates a new ExposeToHttpEndpointAttribute instance
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="route">Route template</param>
    public ExposeToHttpEndpointAttribute(HttpVerb method, string? route = null)
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

