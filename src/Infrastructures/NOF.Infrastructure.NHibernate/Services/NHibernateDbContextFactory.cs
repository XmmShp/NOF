using NOF.Application;
using NOF.Infrastructure;

namespace NOF.Infrastructure.NHibernate;

internal sealed class NHibernateDbContextFactory(
    NHibernateSessionFactoryRegistry sessionFactoryRegistry,
    ICurrentTenant currentTenant) : IDbContextFactory
{
    public IDbContext CreateDbContext()
        => new NHibernateDbContextAdapter(sessionFactoryRegistry.OpenSession(TenantId.Normalize(currentTenant.TenantId)));

    public IDbContext CreateDbContext(string tenantId)
        => new NHibernateDbContextAdapter(sessionFactoryRegistry.OpenSession(TenantId.Normalize(tenantId)));
}
