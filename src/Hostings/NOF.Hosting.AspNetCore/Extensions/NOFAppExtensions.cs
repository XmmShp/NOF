using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Contract;
using System.Text.Json;

namespace NOF.Hosting.AspNetCore;

public static class NOFHostingAspNetCoreExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder ConfigureJsonOptions()
        {
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                var nof = JsonSerializerOptions.NOF;
                options.SerializerOptions.PropertyNameCaseInsensitive = nof.PropertyNameCaseInsensitive;
                options.SerializerOptions.DefaultIgnoreCondition = nof.DefaultIgnoreCondition;
                options.SerializerOptions.ReferenceHandler = nof.ReferenceHandler;
                options.SerializerOptions.PropertyNamingPolicy = nof.PropertyNamingPolicy;

                foreach (var converter in nof.Converters)
                {
                    options.SerializerOptions.Converters.Add(converter);
                }

                options.SerializerOptions.TypeInfoResolver = nof.TypeInfoResolver;
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
            builder.Services.AddOptions<CorsSettingsOptions>();
            builder.Services.AddCors();
            builder.AddInitializationStep(new CorsInitializationStep());
            return builder;
        }

        public INOFAppBuilder UseScalar()
        {
            builder.Services.AddOpenApi(opt =>
            {
                opt.AddSchemaTransformer<OptionalSchemaTransformer>();
            });
            builder.AddInitializationStep(new ScalarInitializationStep());
            return builder;
        }
    }

    extension(WebApplication app)
    {
        public WebApplication MapServiceToHttpEndpoints<TService>(string prefix = "")
            where TService : IRpcService
        {
            return app;
        }
    }
}
