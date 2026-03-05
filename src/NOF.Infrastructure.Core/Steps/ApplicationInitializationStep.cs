using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure.Abstraction;

public class ApplicationInitializationStep : IApplicationInitializationStep<ApplicationInitializationStep>
{
    private readonly Func<IHostApplicationBuilder, IHost, Task> _configurator;

    public ApplicationInitializationStep(Func<IHostApplicationBuilder, IHost, Task> configurator)
    {
        _configurator = configurator;
    }

    public Task ExecuteAsync(IHostApplicationBuilder context, IHost app)
    {
        return _configurator(context, app);
    }
}
