using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure.NHibernate;

internal sealed class NHibernateDbContextFactory(
    NHibernateSessionFactoryRegistry sessionFactoryRegistry) : IDbContextFactory
{
    public IDbContext CreateDbContext()
        => new NHibernateDbContextAdapter(sessionFactoryRegistry.OpenSession(TenantId.Normalize(Context.Current.TenantId)));

    public IDbContext CreateDbContext(string tenantId)
        => new NHibernateDbContextAdapter(sessionFactoryRegistry.OpenSession(TenantId.Normalize(tenantId)));
}
