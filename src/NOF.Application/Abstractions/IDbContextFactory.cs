namespace NOF.Application;

/// <summary>
/// Creates provider-agnostic database contexts.
/// </summary>
public interface IDbContextFactory
{
    IDbContext CreateDbContext();

    IDbContext CreateDbContext(string tenantId);
}
