using MassTransit;
using MassTransit.Internals;
using MassTransit.Metadata;
using MassTransit.Util;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

namespace NOF;

public interface IConfiguringParametersTask : IRegistrationTask;

public interface IConfiguringOptionsTask : IRegistrationTask, IDepsOn<IConfiguringParametersTask>;

public class AddJwtTask : IConfiguringOptionsTask
{
    public Task ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddOptionsInConfiguration<JwtOptions>();
        return Task.CompletedTask;
    }
}

public class AddCorsTask : IConfiguringOptionsTask
{
    public Task ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddOptionsInConfiguration<CorsSettingsOptions>();
        return Task.CompletedTask;
    }
}

public interface IConfiguredOptionsTask : IRegistrationTask, IDepsOn<IConfiguringOptionsTask>;

public class ConfigureJsonOptionsTask : IConfiguredOptionsTask
{
    public Task ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.Converters.Add(OptionalConverterFactory.Instance);
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });
        return Task.CompletedTask;
    }
}

public class ConfigureJwtTask : IConfiguredOptionsTask
{
    public Task ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddScoped<IUserContext, UserContext>();
        args.Builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        args.Builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer();
        args.Builder.Services.AddAuthorization();
        args.Builder.Services.AddScoped<JwtUserInfoMiddleware>();
        args.Builder.Services.AddScoped<PermissionAuthorizationMiddleware>();
        return Task.CompletedTask;
    }
}

public interface IConfiguringServicesTask : IRegistrationTask, IDepsOn<IConfiguredOptionsTask>;

public class AddApiResponseMiddlewareTask : IConfiguringServicesTask
{
    public Task ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddScoped<ApiResponseMiddleware>();
        return Task.CompletedTask;
    }
}

public class AddScalarTask : IConfiguringServicesTask
{
    public Task ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddOpenApi(opt =>
        {
            opt.AddDocumentTransformer<BearerSecuritySchemeTransformer>()
                .AddSchemaTransformer<OptionalSchemaTransformer>();
        });
        return Task.CompletedTask;
    }
}

public class AddDefaultServicesTask : IConfiguringServicesTask
{
    public Task ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddScoped<ICommandSender, CommandSender>();
        args.Builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
        args.Builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        return Task.CompletedTask;
    }
}

public class AddSignalRTask : IConfiguringServicesTask
{
    public Task ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddSignalR();
        return Task.CompletedTask;
    }
}

public class AddAspireTask : IConfiguringServicesTask
{
    public Task ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.AddServiceDefaults();
        return Task.CompletedTask;
    }
}

public class AddRedisDistributedCacheTask : IConfiguringServicesTask
{
    public Task ExecuteAsync(RegistrationArgs args)
    {
        const string redisKey = "redis";
        args.Builder.AddRedisDistributedCache(redisKey);
        return Task.CompletedTask;
    }
}

public class AddPostgreSQLTask<TDbContext> : IConfiguringServicesTask
    where TDbContext : NOFDbContext
{
    public Task ExecuteAsync(RegistrationArgs args)
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
        return Task.CompletedTask;
    }
}

public interface IConfiguredServicesTask : IRegistrationTask, IDepsOn<IConfiguringServicesTask>;

public class AddMassTransitTask : IConfiguredServicesTask
{
    public async Task ExecuteAsync(RegistrationArgs args)
    {
        var consumers = (await AssemblyTypeCache.FindTypes(args.Metadata.Assemblies,
            TypeClassification.Concrete | TypeClassification.Closed,
            RegistrationMetadata.IsConsumerOrDefinition
        )).ToArray();

        var internals = consumers.Where(t => t.HasInterface<IRequestHandler>() || t.HasInterface<IEventHandler>());
        var externals = consumers.Where(t => t.HasInterface<ICommandHandler>() || t.HasInterface<INotificationHandler>());

        args.Builder.Services.AddMediator(config =>
        {
            config.AddConsumers(internals.ToArray());
        });

        const string rabbitMQKey = "rabbitmq";
        var configurators = args.Metadata.MassTransitConfigurations;
        args.Builder.Services.AddMassTransit(config =>
        {
            config.SetEndpointNameFormatter(EndpointNameFormatter.Instance);
            config.AddConsumers(externals.ToArray());
            config.UsingRabbitMq((context, cfg) =>
            {
                var connectionString = args.Builder.Configuration.GetConnectionString(rabbitMQKey) ?? throw new InvalidOperationException();
                cfg.Host(new Uri(connectionString));
                cfg.ConfigureEndpoints(context);
            });
            foreach (var configurator in configurators)
            {
                configurator(config);
            }
        });
    }
}

public interface ISyncSeedTask : IStartupTask;

public class MigrationTask : ISyncSeedTask
{
    public async Task ExecuteAsync(StartupArgs args)
    {
        await using var scope = args.App.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
        }
    }
}

public interface IObservabilityTask : IStartupTask, IDepsOn<ISyncSeedTask>;
public interface ISecurityTask : IStartupTask, IDepsOn<IObservabilityTask>;

public class UseCorsTask : ISecurityTask
{
    public Task ExecuteAsync(StartupArgs args)
    {
        var corsSettings = args.App.Services.GetRequiredService<IOptions<CorsSettingsOptions>>().Value;

        args.App.UseCors(policy =>
        {
            policy.WithOrigins(corsSettings.AllowedOrigins)
                .WithMethods(corsSettings.AllowedMethods)
                .WithHeaders(corsSettings.AllowedHeaders);
            if (corsSettings.AllowCredentials)
            {
                policy.AllowCredentials();
            }
        });

        return Task.CompletedTask;
    }
}

public interface IAuthenticationTask : IStartupTask, IDepsOn<ISecurityTask>;

public class UseJwtTask : IAuthenticationTask
{
    public Task ExecuteAsync(StartupArgs args)
    {
        args.App.UseAuthentication();
        args.App.UseMiddleware<JwtUserInfoMiddleware>();
        args.App.UseMiddleware<PermissionAuthorizationMiddleware>();
        args.App.UseAuthorization();
        return Task.CompletedTask;
    }
}

public interface IBusinessTask : IStartupTask, IDepsOn<IAuthenticationTask>;

public class UseApiResponseTask : IBusinessTask
{
    public Task ExecuteAsync(StartupArgs args)
    {
        args.App.UseMiddleware<ApiResponseMiddleware>();
        return Task.CompletedTask;
    }
}

public interface IEndpointTask : IStartupTask, IDepsOn<IBusinessTask>;

public class UseAspireTask : IEndpointTask
{
    public Task ExecuteAsync(StartupArgs args)
    {
        args.App.MapDefaultEndpoints();
        return Task.CompletedTask;
    }
}

public class UseScalarTask : IEndpointTask
{
    public Task ExecuteAsync(StartupArgs args)
    {
        if (args.App.Environment.IsDevelopment())
        {
            args.App.MapOpenApi();
            args.App.MapScalarApiReference();
        }
        return Task.CompletedTask;
    }
}
