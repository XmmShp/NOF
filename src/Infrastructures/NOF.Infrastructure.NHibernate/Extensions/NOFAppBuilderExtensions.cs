using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NHibernate;
using NOF.Application;
using NOF.Hosting;
using NOF.Infrastructure;

namespace NOF.Infrastructure.NHibernate;

public static partial class NOFInfrastructureExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public NHibernateSelector UseNHibernate()
        {
            builder.Services.AddOptions<NHibernateConfigurationOptions>();
            builder.Services.TryAddSingleton<NHibernateSessionFactoryRegistry>();
            builder.Services.ReplaceOrAddScoped<IDbContextFactory, NHibernateDbContextFactory>();
            builder.Services.ReplaceOrAddScoped(sp =>
                sp.GetRequiredService<NHibernateSessionFactoryRegistry>()
                    .OpenSession(TenantId.Normalize(sp.GetRequiredService<ICurrentTenant>().TenantId)));
            builder.Services.ReplaceOrAddScoped<IDbContext>(sp =>
                new NHibernateDbContextAdapter(sp.GetRequiredService<ISession>()));

            return new NHibernateSelector(builder);
        }
    }
}
