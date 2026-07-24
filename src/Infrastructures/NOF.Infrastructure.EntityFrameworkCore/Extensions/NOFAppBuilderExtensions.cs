using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NOF.Application;
using NOF.Infrastructure.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

public static partial class NOFInfrastructureExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddNOFEntityFrameworkCore()
        {
            builder.Services.AddOptions<DbContextConfigurationOptions>();
            builder.Services.TryAddSingleton<SqliteInMemoryConnectionKeeper>();

            builder.UseDbContext<NOFDbContext>()
                .WithConnectionString("Data Source=nof-sqlite-memory-{tenantId};Mode=Memory;Cache=Shared")
                .WithTenantMode(TenantMode.DatabasePerTenant)
                .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseSqlite(connectionString));

            return builder;
        }

        public EFCoreSelector UseDbContext<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TDbContext>()
            where TDbContext : NOFDbContext
        {
            builder.Services.AddOptions<DbContextConfigurationOptions>();
            builder.Services.TryAddSingleton<SqliteInMemoryConnectionKeeper>();
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Scoped<NOFDbContextFactory<TDbContext>, NOFDbContextFactory<TDbContext>>());
            builder.Services.ReplaceOrAddScoped<IDbContextFactory>(sp => sp.GetRequiredService<NOFDbContextFactory<TDbContext>>());
            builder.Services.ReplaceOrAddScoped<IDbContextFactory<TDbContext>, TypedDbContextFactory<TDbContext>>();
            builder.Services.ReplaceOrAddScoped<NOFDbContext>(sp => sp.GetRequiredService<NOFDbContextFactory<TDbContext>>().CreateConcreteDbContext());
            builder.Services.ReplaceOrAddScoped<DbContext>(sp => sp.GetRequiredService<NOFDbContext>());
            builder.Services.ReplaceOrAddScoped(sp => sp.GetRequiredService<IDbContextFactory>().CreateDbContext());
            builder.Services.AddRepositoryProviders();
            if (typeof(TDbContext) != typeof(NOFDbContext))
            {
                builder.Services.ReplaceOrAddScoped(sp => sp.GetRequiredService<NOFDbContextFactory<TDbContext>>().CreateConcreteDbContext());
            }

            return new EFCoreSelector(builder, typeof(TDbContext));
        }

    }
}
