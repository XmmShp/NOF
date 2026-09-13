using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NHibernate;
using NOF.Application;
using NOF.Contract;

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
                    .OpenSession(TenantId.Normalize(Context.Current.TenantId)));
            builder.Services.ReplaceOrAddScoped<IDbContext>(sp =>
                new NHibernateDbContextAdapter(sp.GetRequiredService<ISession>()));
            builder.Services.AddRepositoryProviders();

            return new NHibernateSelector(builder);
        }
    }
}
