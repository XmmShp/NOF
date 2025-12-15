using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NOF;

public class CorsConfig<THostApplication> : ISecurityConfig<THostApplication>
    where THostApplication : class, IHost, IApplicationBuilder
{
    public Task ExecuteAsync(INOFAppBuilder<THostApplication> builder, THostApplication app)
    {
        var corsSettings = app.Services.GetRequiredService<IOptions<CorsSettingsOptions>>().Value;
        app.UseCors(policy =>
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