namespace NOF.Hosting.AspNetCore;

/// <summary>
/// Declares that the specified RPC service should be exposed as HTTP endpoints
/// when its assembly is added via <c>AddApplicationPart(...)</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class MapServiceToHttpEndpointsAttribute<TService> : Attribute
    where TService : class, Contract.IRpcService
{
    /// <summary>
    /// Gets or sets an optional route prefix.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;
}
