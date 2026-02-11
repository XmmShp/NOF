using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Infrastructure.Core;
using System.Text.Json.Serialization;

namespace NOF.Hosting.AspNetCore;

/// <summary>
/// Event published when OpenAPI is being configured
/// Allows users to customize OpenAPI configuration
/// </summary>
public sealed record OpenApiConfigurating(OpenApiOptions Options);

public static partial class NOFHostingAspNetCoreExtensions
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

        public INOFAppBuilder UseDefaultSettings()
        {
            builder.ConfigureJsonOptions();
            builder.UseCors();

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

        public INOFAppBuilder UseScalar()
        {
            builder.Services.AddOpenApi(opt =>
            {
                builder.StartupEventChannel.Publish(new OpenApiConfigurating(opt));

                opt.AddSchemaTransformer<OptionalSchemaTransformer>();
            });
            builder.AddInitializationStep(new ScalarInitializationStep());
            return builder;
        }
    }
}
