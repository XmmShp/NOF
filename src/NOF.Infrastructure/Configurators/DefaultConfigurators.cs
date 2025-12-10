using MassTransit;
using MassTransit.Internals;
using MassTransit.Metadata;
using MassTransit.Util;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

namespace NOF;

public class AddJwtAuthenticationConfigurator : ICombinedConfigurator, IConfiguredOptionsConfigurator, IAuthenticationConfigurator
{
    public ValueTask ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddOptionsInConfiguration<JwtOptions>();
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
        return ValueTask.CompletedTask;
    }

    public Task ExecuteAsync(StartupArgs args)
    {
        args.App.UseAuthentication();
        args.App.UseMiddleware<JwtUserInfoMiddleware>();
        args.App.UseMiddleware<PermissionAuthorizationMiddleware>();
        args.App.UseAuthorization();
        return Task.CompletedTask;
    }
}

public class AddCorsConfigurator : ICombinedConfigurator, IConfiguringOptionsConfigurator, ISecurityConfigurator
{
    public ValueTask ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddOptionsInConfiguration<CorsSettingsOptions>();
        args.Builder.Services.AddCors();
        return ValueTask.CompletedTask;
    }

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

public class ConfigureJsonOptionsConfigurator : IConfiguredOptionsConfigurator
{
    public ValueTask ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.Converters.Add(OptionalConverterFactory.Instance);
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });
        return ValueTask.CompletedTask;
    }
}

public class AddApiResponseMiddlewareConfigurator : ICombinedConfigurator, IConfiguringServicesConfigurator, IResponseWrapConfigurator
{
    public ValueTask ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddScoped<ApiResponseMiddleware>();
        return ValueTask.CompletedTask;
    }

    public Task ExecuteAsync(StartupArgs args)
    {
        args.App.UseMiddleware<ApiResponseMiddleware>();
        return Task.CompletedTask;
    }
}

public class AddScalarConfigurator : ICombinedConfigurator, IConfiguringServicesConfigurator, IEndpointConfigurator
{
    public ValueTask ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddOpenApi(opt =>
        {
            opt.AddDocumentTransformer<BearerSecuritySchemeTransformer>()
                .AddSchemaTransformer<OptionalSchemaTransformer>();
        });
        return ValueTask.CompletedTask;
    }

    public Task ExecuteAsync(StartupArgs args)
    {
        args.App.MapOpenApi();
        args.App.MapScalarApiReference();
        return Task.CompletedTask;
    }
}

public class AddDefaultServicesConfigurator : IConfiguringServicesConfigurator
{
    public ValueTask ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddScoped<ICommandSender, CommandSender>();
        args.Builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
        return ValueTask.CompletedTask;
    }
}

public class AddSignalRConfigurator : IConfiguringServicesConfigurator
{
    public ValueTask ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddSignalR();
        return ValueTask.CompletedTask;
    }
}

public class AddAspireConfigurator : ICombinedConfigurator, IConfiguringServicesConfigurator, IEndpointConfigurator
{
    public ValueTask ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.AddServiceDefaults();
        return ValueTask.CompletedTask;
    }

    public Task ExecuteAsync(StartupArgs args)
    {
        args.App.MapDefaultEndpoints();
        return Task.CompletedTask;
    }
}

public class AddRedisDistributedCacheConfigurator : IConfiguringServicesConfigurator
{
    public ValueTask ExecuteAsync(RegistrationArgs args)
    {
        const string redisKey = "redis";
        args.Builder.AddRedisDistributedCache(redisKey);
        return ValueTask.CompletedTask;
    }
}


public class AddMassTransitConfigurator : IConfiguredServicesConfigurator
{
    public async ValueTask ExecuteAsync(RegistrationArgs args)
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