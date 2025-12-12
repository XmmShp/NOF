using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace NOF;

public static partial class __NOF_Host_Extensions__
{
    extension(INOFApp app)
    {
        public INOFApp UseDefaultSettings()
        {
            app.ConfigureJsonOptions();
            app.UseCors();
            app.UseJwtAuthentication();
            app.UseSignalR();
            app.UseRedisDistributedCache();
            app.UseResponseWrapper();

            if (app.Unwrap().Environment.IsDevelopment())
            {
                app.UseScalar();
            }
            return app;
        }

        public INOFApp UseCors()
        {
            app.Services.AddOptionsInConfiguration<CorsSettingsOptions>();
            app.Services.AddCors();
            app.AddStartupConfigurator<CorsConfigurator>();
            return app;
        }

        public INOFApp ConfigureJsonOptions()
        {
            app.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.PropertyNameCaseInsensitive = true;
                options.SerializerOptions.Converters.Add(OptionalConverterFactory.Instance);
                options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });
            return app;
        }

        public INOFApp UseJwtAuthentication()
        {
            app.Services.AddOptionsInConfiguration<JwtOptions>();
            app.Services.AddScoped<IUserContext, UserContext>();
            app.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
            app.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer();
            app.Services.AddAuthorization();
            app.Services.AddScoped<JwtUserInfoMiddleware>();
            app.Services.AddScoped<PermissionAuthorizationMiddleware>();
            app.AddStartupConfigurator<JwtAuthenticationConfigurator>();
            return app;
        }

        public INOFApp UseResponseWrapper()
        {
            app.Services.AddScoped<ResponseWrapperMiddleware>();
            app.AddStartupConfigurator<ResponseWrapperConfigurator>();
            return app;
        }

        public INOFApp UseScalar()
        {
            app.Services.AddOpenApi(opt =>
            {
                opt.AddDocumentTransformer<BearerSecuritySchemeTransformer>()
                    .AddSchemaTransformer<OptionalSchemaTransformer>();
            });
            app.AddStartupConfigurator<ScalarConfigurator>();
            return app;
        }

        public INOFApp UseSignalR()
        {
            app.Services.AddSignalR();
            return app;
        }

        public INOFApp UseRedisDistributedCache()
        {
            const string redisKey = "redis";
            app.Unwrap().AddRedisDistributedCache(redisKey);
            return app;
        }
    }
}

internal class DelegateStartupConfigurator : IBusinessConfigurator
{
    private readonly Func<INOFApp, WebApplication, Task> _fn;

    public DelegateStartupConfigurator(Func<INOFApp, WebApplication, Task> func)
    {
        _fn = func;
    }

    public Task ExecuteAsync(INOFApp app, WebApplication webApp)
    {
        return _fn(app, webApp);
    }
}

internal class DelegateRegistrationConfigurator : IConfiguredServicesConfigurator
{
    private readonly Func<INOFApp, ValueTask> _fn;

    public DelegateRegistrationConfigurator(Func<INOFApp, ValueTask> func)
    {
        _fn = func;
    }

    public ValueTask ExecuteAsync(INOFApp app)
    {
        return _fn(app);
    }
}