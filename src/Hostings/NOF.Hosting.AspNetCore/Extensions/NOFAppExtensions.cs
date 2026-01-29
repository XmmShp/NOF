using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json.Serialization;

namespace NOF;

public static partial class __NOF_Hosting_AspNetCore_Extensions__
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder ConfigureJsonOptions()
        {
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.PropertyNameCaseInsensitive = true;
                options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });
            return builder;
        }

        public INOFAppBuilder UseSignalR()
        {
            builder.Services.AddSignalR();
            return builder;
        }

        public INOFAppBuilder UseDefaultSettings()
        {
            builder.ConfigureJsonOptions();
            builder.UseCors();
            builder.UseJwtAuthentication();
            builder.UseSignalR();

            if (builder.Environment.IsDevelopment())
            {
                builder.UseScalar();
            }
            return builder;
        }

        public INOFAppBuilder UseCors()
        {
            builder.Services.AddOptionsInConfiguration<CorsSettingsOptions>();
            builder.Services.AddCors();
            builder.AddInitializationStep(new CorsInitializationStep());
            return builder;
        }

        public INOFAppBuilder UseJwtAuthentication()
        {
            builder.Services.AddOptionsInConfiguration<JwtOptions>();
            builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer();
            builder.Services.AddAuthorization();
            builder.Services.AddScoped<JwtAuthenticationContextMiddleware>();
            builder.AddInitializationStep(new JwtAuthenticationInitializationStep());
            return builder;
        }

        public INOFAppBuilder UseScalar()
        {
            builder.Services.AddOpenApi(opt =>
            {
                opt.AddDocumentTransformer<BearerSecuritySchemeTransformer>()
                    .AddSchemaTransformer<OptionalSchemaTransformer>();
            });
            builder.AddInitializationStep(new ScalarInitializationStep());
            return builder;
        }
    }
}