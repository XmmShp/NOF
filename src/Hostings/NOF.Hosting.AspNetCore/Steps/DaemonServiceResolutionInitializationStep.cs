using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using NOF.Infrastructure;

namespace NOF.Hosting.AspNetCore;

public sealed class DaemonServiceResolutionInitializationStep : IApplicationInitializationStep
{
    public TopologyComparison Compare(IApplicationInitializationStep other)
        => other is RpcServerTransportInitializationStep
            ? TopologyComparison.Before
            : TopologyComparison.DoesNotMatter;

    public Task ExecuteAsync(IHost app)
    {
        if (app is IApplicationBuilder actualApp)
        {
            actualApp.UseMiddleware<DaemonServiceResolutionMiddleware>();
        }

        return Task.CompletedTask;
    }
}
