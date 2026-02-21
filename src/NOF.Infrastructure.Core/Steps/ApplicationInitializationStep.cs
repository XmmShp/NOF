using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure.Abstraction;

public class ApplicationInitializationStep : IApplicationInitializationStep
{
    private readonly Func<IApplicationInitializationContext, IHost, Task> _configurator;

    public ApplicationInitializationStep(Func<IApplicationInitializationContext, IHost, Task> configurator)
    {
        _configurator = configurator;
    }

    public Task ExecuteAsync(IApplicationInitializationContext context, IHost app)
    {
        return _configurator(context, app);
    }
}
