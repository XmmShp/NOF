using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

namespace NOF;

public class ScalarConfig<THostApplication> : IEndpointConfig<THostApplication>
    where THostApplication : class, IHost, IEndpointRouteBuilder
{
    public Task ExecuteAsync(INOFAppBuilder builder, THostApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
        return Task.CompletedTask;
    }
}