using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NOF;

public static partial class __NOF_Infrastructure__EntityFrameworkCore__PostgreSQL__
{
    extension<TDbContext>(INOFEFCoreApp<TDbContext> app)
        where TDbContext : NOFDbContext
    {
        public INOFApp UsePostgreSQL()
        {
            app.App.AddRegistrationConfigurator<AddPostgreSQLConfigurator<TDbContext>>();
            return app.App;
        }
    }
}

public class AddPostgreSQLConfigurator<TDbContext> : IConfiguringServicesConfigurator
    where TDbContext : NOFDbContext
{
    public ValueTask ExecuteAsync(RegistrationArgs args)
    {
        const string postgresKey = "postgres";
        args.Builder.Services.AddDbContext<TDbContext>(options => options.UseNpgsql(args.Builder.Configuration.GetConnectionString(postgresKey)));
        args.Builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());
        args.Metadata.MassTransitConfigurations.Add(config =>
        {
            config.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });
            config.AddConfigureEndpointsCallback((context, name, cfg) =>
            {
                cfg.UseEntityFrameworkOutbox<TDbContext>(context);
            });
        });
        return ValueTask.CompletedTask;
    }
}
