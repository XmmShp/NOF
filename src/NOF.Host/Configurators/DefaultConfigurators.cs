using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NOF.Infrastructure.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]

namespace NOF;

public class CorsConfigurator : ISecurityConfigurator
{
    public Task ExecuteAsync(INOFApp app, WebApplication webApp)
    {
        var corsSettings = webApp.Services.GetRequiredService<IOptions<CorsSettingsOptions>>().Value;

        webApp.UseCors(policy =>
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

public class ResponseWrapperConfigurator : IResponseWrapConfigurator
{
    public Task ExecuteAsync(INOFApp app, WebApplication webApp)
    {
        webApp.UseMiddleware<ResponseWrapperMiddleware>();
        return Task.CompletedTask;
    }
}

public class ScalarConfigurator : IEndpointConfigurator
{
    public Task ExecuteAsync(INOFApp app, WebApplication webApp)
    {
        webApp.MapOpenApi();
        webApp.MapScalarApiReference();
        return Task.CompletedTask;
    }
}
