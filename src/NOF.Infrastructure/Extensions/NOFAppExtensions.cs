using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using System.Reflection;

namespace NOF;

public static class NOFAppBuilderExtensions
{
    extension(NOFAppBuilder app)
    {
        #region Easy Properties
        public bool UseAspire
        {
            get => app.GetOrSetMetadata("UseAspire", () => false);
            set => app.SetMetadata("UseAspire", value);
        }

        public bool UseSignalR
        {
            get => app.GetOrSetMetadata("UseSignalR", () => false);
            set => app.SetMetadata("UseSignalR", value);
        }

        public bool UseCors
        {
            get => app.GetOrSetMetadata("UseCors", () => false);
            set => app.SetMetadata("UseCors", value);
        }

        public bool UseJwtAuthentication
        {
            get => app.GetOrSetMetadata("UseJwtAuthentication", () => false);
            set => app.SetMetadata("UseJwtAuthentication", value);
        }

        public bool UseDistributedCache
        {
            get => app.GetOrSetMetadata("UseDistributedCache", () => false);
            set => app.SetMetadata("UseDistributedCache", value);
        }

        public bool UseScalar
        {
            get => app.GetOrSetMetadata("UseScalar", () => false);
            set => app.SetMetadata("UseScalar", value);
        }

        public bool UseDatabase
        {
            get => app.GetOrSetMetadata("UseDatabase", () => false);
            set => app.SetMetadata("UseDatabase", value);
        }
        #endregion

        #region Metadata
        public T GetRequiredMetadata<T>(string name)
            => app.GetMetadata<T>(name) ?? throw new NullReferenceException();
        #endregion

        #region AssemblyManage
        public NOFAppBuilder AddAssembly<T>()
            => app.AddAssembly(typeof(T).Assembly);

        public NOFAppBuilder AddAssembly(Assembly assembly)
        {
            app.Assemblies.Add(assembly);
            return app;
        }
        #endregion

        #region DefaultConfiguration

        public NOFAppBuilder UseDefaultSettings()
        {
            app.AddJwtAuthentication();
            app.AddAspire();
            app.AddSignalR();
            app.AddCors();
            app.AddRedis();
            app.AddScalarInDevelopment();
            return app;
        }

        #endregion

        #region Setup Helper
        public NOFAppBuilder AddJwtAuthentication()
        {
            app.UseJwtAuthentication = true;
            app.AddAction(builder =>
            {
                builder.Services.AddScoped<IUserContext, UserContext>();
                builder.Services.AddOptionsInConfiguration<JwtOptions>();
                builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
                builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                }).AddJwtBearer();
                builder.Services.AddAuthorization();
                builder.Services.AddScoped<JwtUserInfoMiddleware>();
                builder.Services.AddScoped<PermissionAuthorizationMiddleware>();
            });
            app.ConfigureApp(app =>
            {
                app.UseAuthentication();
                app.UseMiddleware<JwtUserInfoMiddleware>();
                app.UseMiddleware<PermissionAuthorizationMiddleware>();
                app.UseAuthorization();
            });
            return app;
        }

        public NOFAppBuilder AddAspire()
        {
            app.UseAspire = true;
            app.AddAction(builder => builder.AddServiceDefaults());
            app.ConfigureApp(app => app.MapDefaultEndpoints());
            return app;
        }

        public NOFAppBuilder AddSignalR()
        {
            app.UseSignalR = true;
            app.AddAction(builder => builder.Services.AddSignalR());
            return app;
        }

        public NOFAppBuilder AddCors()
        {
            const string corsPolicyName = "__NOFCorsPolicy__";
            const string corsSettingsAllowOriginsKey = "CorsSettings:AllowedOrigins";

            app.UseCors = true;
            app.AddAction(builder => builder.Services.AddCors(options =>
            {
                options.AddPolicy(corsPolicyName, policy =>
                {
                    var clientUrls =
                        builder.Configuration.GetSection(corsSettingsAllowOriginsKey).Get<string[]>()
                        ?? [];

                    policy.WithOrigins(clientUrls)
                                    .AllowAnyMethod()
                                    .AllowAnyHeader()
                                    .AllowCredentials();
                });
            }));

            app.ConfigureApp(app =>
            {
                app.UseCors(corsPolicyName);
            });
            return app;
        }

        public NOFAppBuilder AddRedis()
        {
            const string redis = "redis";
            app.UseDistributedCache = true;
            app.AddAction(builder => builder.AddRedisDistributedCache(redis));
            return app;
        }

        public NOFAppBuilder AddScalarInDevelopment()
        {
            app.UseScalar = true;

            app.AddAction(builder =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    builder.Services.AddOpenApi(opt =>
                    {
                        opt.AddDocumentTransformer<BearerSecuritySchemeTransformer>()
                            .AddSchemaTransformer<OptionalSchemaTransformer>();
                    });
                }
            });

            app.ConfigureApp(app =>
            {
                if (app.Environment.IsDevelopment())
                {
                    app.MapOpenApi();
                    app.MapScalarApiReference();
                }
            });

            return app;
        }

        public NOFAppBuilder AddPostgres<TDbContext>(bool autoMigrate = false)
            where TDbContext : DbContext
        {
            const string postgresConnectStringKey = "postgres";

            app.UseDatabase = true;
            app.AddAction(builder =>
                {
                    builder.Services.AddDbContext<TDbContext>(options =>
                        options.UseNpgsql(builder.Configuration.GetConnectionString(postgresConnectStringKey)));
                    builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());

                });
            app.ConfigureConsumePipe((cfg, context) =>
            {
                cfg.UseConsumeFilter(
                    typeof(SaveChangesFilter<>),
                    context
                );
            });
            if (autoMigrate)
            {
                app.ConfigureApp(app =>
                {
                    using var scope = app.Services.CreateAsyncScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
                    if (dbContext.Database.IsRelational())
                    {
                        dbContext.Database.Migrate();
                    }
                });
            }
            return app;
        }

        public ITemporaryBus GetTemporaryBus()
        {
            var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                var connectionString = app.RabbitMQConnectionString
                                       ?? throw new InvalidOperationException();
                cfg.Host(new Uri(connectionString));
            });

            var disposableBus = new TemporaryBus(bus);
            return disposableBus;
        }
        #endregion
    }
}
