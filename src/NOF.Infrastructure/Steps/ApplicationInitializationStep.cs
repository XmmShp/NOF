using Microsoft.Extensions.Hosting;
using NOF.Hosting;

namespace NOF.Infrastructure;

public class ApplicationInitializationStep : IApplicationInitializationStep
{
    public TopologyComparison Compare(IApplicationInitializationStep other) => TopologyComparison.DoesNotMatter;

    private readonly Func<IHost, Task> _configurator;

    public ApplicationInitializationStep(Func<IHost, Task> configurator)
    {
        _configurator = configurator;
    }

    public Task ExecuteAsync(IHost app)
    {
        return _configurator(app);
    }
}
