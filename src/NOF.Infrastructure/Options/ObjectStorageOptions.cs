using NOF.Application;

namespace NOF.Infrastructure;

/// <summary>
/// Configures the current <see cref="IObjectStorage"/> registration.
/// </summary>
public sealed class ObjectStorageOptions
{
    /// <summary>
    /// Gets or sets the prefix applied to every logical object key.
    /// The <c>{tenantId}</c> placeholder is resolved from the current tenant.
    /// </summary>
    /// <example><c>tenants/{tenantId}/</c></example>
    public string? KeyPrefix { get; set; }
}
