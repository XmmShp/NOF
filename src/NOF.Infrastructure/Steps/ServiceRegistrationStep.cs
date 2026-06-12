using Microsoft.Extensions.Hosting;
using NOF.Hosting;

namespace NOF.Infrastructure;

public class ServiceRegistrationStep : IServiceRegistrationStep
{
    public TopologyComparison Compare(IServiceRegistrationStep other) => TopologyComparison.DoesNotMatter;

    private readonly Func<IHostApplicationBuilder, ValueTask> _configurator;

    public ServiceRegistrationStep(Func<IHostApplicationBuilder, ValueTask> configurator)
    {
        _configurator = configurator;
    }

    public ValueTask ExecuteAsync(IHostApplicationBuilder builder)
    {
        return _configurator(builder);
    }
}
