using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Application;
using NOF.Infrastructure;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

public static partial class NOFInfrastructureExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddEntityFrameworkCoreDefaults()
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
            builder.Services.ReplaceOrAddScoped<INOFDbContextFactory<TDbContext>, NOFDbContextFactory<TDbContext>>();
            builder.Services.ReplaceOrAddScoped<IDbContextFactory<TDbContext>, DbContextFactory<TDbContext>>();
            builder.Services.ReplaceOrAddScoped<INOFDbContextFactory>(sp => sp.GetRequiredService<INOFDbContextFactory<TDbContext>>());
            builder.Services.ReplaceOrAddScoped<NOFDbContext>(sp => sp.GetRequiredService<INOFDbContextFactory<TDbContext>>().CreateDbContext());
            builder.Services.ReplaceOrAddScoped<DbContext>(sp => sp.GetRequiredService<NOFDbContext>());
            builder.Services.ReplaceOrAddScoped<IDbContext>(sp => new EfCoreDbContextAdapter(sp.GetRequiredService<DbContext>()));
            builder.Services.ReplaceOrAddSingleton<IInboxMessageStore, InboxMessageStore>();
            if (typeof(TDbContext) != typeof(NOFDbContext))
            {
                builder.Services.ReplaceOrAddScoped(sp => sp.GetRequiredService<INOFDbContextFactory<TDbContext>>().CreateDbContext());
            }

            return new EFCoreSelector(builder, typeof(TDbContext));
        }

        public INOFAppBuilder AddDbContextModelCreating(Action<ModelBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            builder.Services.AddOptions<DbContextConfigurationOptions>();
            builder.Services.AddSingleton<INOFDbContextModelCreatingContributor>(
                new DelegateDbContextModelCreatingContributor(configure));
            return builder;
        }
    }
}
