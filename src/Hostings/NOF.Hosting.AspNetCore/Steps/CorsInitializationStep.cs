using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NOF;

public class CorsInitializationStep : ISecurityInitializationStep
{
    public Task ExecuteAsync(INOFAppBuilder builder, IHost app)
    {
        if (app is IApplicationBuilder actualApp)
        {
            var corsSettings = app.Services.GetRequiredService<IOptions<CorsSettingsOptions>>().Value;
            actualApp.UseCors(policy =>
            {
                policy.WithOrigins(corsSettings.AllowedOrigins)
                    .WithMethods(corsSettings.AllowedMethods)
                    .WithHeaders(corsSettings.AllowedHeaders);
                if (corsSettings.AllowCredentials)
                {
                    policy.AllowCredentials();
                }
            });
        }

        return Task.CompletedTask;
    }
}
