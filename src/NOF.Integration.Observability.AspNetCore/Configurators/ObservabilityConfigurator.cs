using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace NOF;

public class ObservabilityConfig<THostApplication> : IObservabilityConfig<THostApplication>
    where THostApplication : class, IHost, IEndpointRouteBuilder
{
    public Task ExecuteAsync(INOFAppBuilder<THostApplication> builder, THostApplication app)
    {
        app.MapHealthCheckEndpoints();
        return Task.CompletedTask;
    }
}
