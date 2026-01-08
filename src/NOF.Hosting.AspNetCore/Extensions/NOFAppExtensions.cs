using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
    }

    extension<THostApplication>(INOFAppBuilder<THostApplication> builder)
        where THostApplication : class, IHost, IApplicationBuilder, IEndpointRouteBuilder
    {
        public INOFAppBuilder<THostApplication> UseDefaultSettings()
        {
            builder.ConfigureJsonOptions();
            builder.UseCors();
            builder.UseJwtAuthentication();
            builder.UseSignalR();
            builder.UseResponseWrapper();

            if (builder.Environment.IsDevelopment())
            {
                builder.UseScalar();
            }
            return builder;
        }

        public INOFAppBuilder<THostApplication> UseCors()
        {
            builder.Services.AddOptionsInConfiguration<CorsSettingsOptions>();
            builder.Services.AddCors();
            builder.AddApplicationConfig(new CorsConfig<THostApplication>());
            return builder;
        }

        public INOFAppBuilder<THostApplication> UseJwtAuthentication()
        {
            builder.Services.AddOptionsInConfiguration<JwtOptions>();
            builder.Services.AddScoped<IUserContext, UserContext>();
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
            builder.AddApplicationConfig(new JwtAuthenticationConfig<THostApplication>());
            return builder;
        }

        public INOFAppBuilder<THostApplication> UseResponseWrapper()
        {
            builder.Services.AddScoped<ResponseWrapperMiddleware>();
            builder.AddApplicationConfig(new ResponseWrapperConfig<THostApplication>());
            return builder;
        }

        public INOFAppBuilder<THostApplication> UseScalar()
        {
            builder.Services.AddOpenApi(opt =>
            {
                opt.AddDocumentTransformer<BearerSecuritySchemeTransformer>()
                    .AddSchemaTransformer<OptionalSchemaTransformer>();
            });
            builder.AddApplicationConfig(new ScalarConfig<THostApplication>());
            return builder;
        }
    }
}