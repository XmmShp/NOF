namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// Resolves the connection string used to create a tenant-aware database context.
/// </summary>
/// <param name="context">The current database context resolution context.</param>
/// <returns>The resolved connection string.</returns>
public delegate string DbContextConnectionStringResolver(DbContextConnectionStringResolutionContext context);

/// <summary>
/// Describes a database context connection-string resolution request.
/// </summary>
public sealed class DbContextConnectionStringResolutionContext
{
    /// <summary>
    /// Initializes a database context connection-string resolution request.
    /// </summary>
    /// <param name="services">The scoped service provider creating the database context.</param>
    /// <param name="dbContextType">The concrete database context type.</param>
    /// <param name="tenantId">The normalized tenant identifier.</param>
    /// <param name="tenantMode">The configured tenant mode.</param>
    /// <param name="connectionStringTemplate">The configured fallback connection-string template.</param>
    public DbContextConnectionStringResolutionContext(
        IServiceProvider services,
        Type dbContextType,
        string tenantId,
        TenantMode tenantMode,
        string connectionStringTemplate)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dbContextType);
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(connectionStringTemplate);

        Services = services;
        DbContextType = dbContextType;
        TenantId = tenantId;
        TenantMode = tenantMode;
        ConnectionStringTemplate = connectionStringTemplate;
    }

    /// <summary>
    /// Gets the scoped service provider creating the database context.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets the concrete database context type.
    /// </summary>
    public Type DbContextType { get; }

    /// <summary>
    /// Gets the normalized tenant identifier.
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// Gets the configured tenant mode.
    /// </summary>
    public TenantMode TenantMode { get; }

    /// <summary>
    /// Gets the configured fallback connection-string template.
    /// </summary>
    public string ConnectionStringTemplate { get; }
}
