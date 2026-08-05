using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

internal sealed class RpcServerTransportInitializationStep : IApplicationInitializationStep
{
    public TopologyComparison Compare(IApplicationInitializationStep other)
        => TopologyComparison.DoesNotMatter;

    public async Task ExecuteAsync(IHost app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var registry = app.Services.GetRequiredService<RpcServerRegistry>();
        var registrations = registry.Freeze();
        var transports = new DependencyGraph<IRpcServerTransport>(
            app.Services.GetServices<IRpcServerTransport>()).GetExecutionOrder();

        foreach (var transport in transports)
        {
            foreach (var registration in registrations)
            {
                await transport.MapAsync(app, registration).ConfigureAwait(false);
            }
        }
    }
}
