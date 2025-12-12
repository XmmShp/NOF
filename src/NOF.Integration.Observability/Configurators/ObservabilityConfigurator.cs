using Microsoft.AspNetCore.Builder;

namespace NOF;

public class ObservabilityConfigurator : IObservabilityConfigurator
{
    public Task ExecuteAsync(INOFApp app, WebApplication webApp)
    {
        webApp.MapHealthCheckEndpoints();
        return Task.CompletedTask;
    }
}
