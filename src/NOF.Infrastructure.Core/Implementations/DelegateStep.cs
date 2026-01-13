using Microsoft.Extensions.Hosting;

namespace NOF;

public class ApplicationInitializationStep : IApplicationInitializationStep
{
    private readonly Func<INOFAppBuilder, IHost, Task> _configurator;

    public ApplicationInitializationStep(Func<INOFAppBuilder, IHost, Task> configurator)
    {
        _configurator = configurator;
    }

    public Task ExecuteAsync(INOFAppBuilder builder, IHost app)
    {
        return _configurator(builder, app);
    }
}

public class ServiceRegistrationStep : IServiceRegistrationStep
{
    private readonly Func<INOFAppBuilder, ValueTask> _configurator;

    public ServiceRegistrationStep(Func<INOFAppBuilder, ValueTask> configurator)
    {
        _configurator = configurator;
    }
    public ValueTask ExecuteAsync(INOFAppBuilder builder)
    {
        return _configurator(builder);
    }
}