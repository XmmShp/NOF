namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// Marks an entity as host-only, meaning it should only be stored in the host/public database
/// and not in tenant-specific databases.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class HostOnlyAttribute : Attribute;
