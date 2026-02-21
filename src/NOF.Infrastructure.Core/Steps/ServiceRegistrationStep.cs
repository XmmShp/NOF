namespace NOF.Infrastructure.Abstraction;

public class ServiceRegistrationStep : IServiceRegistrationStep
{
    private readonly Func<IServiceRegistrationContext, ValueTask> _configurator;

    public ServiceRegistrationStep(Func<IServiceRegistrationContext, ValueTask> configurator)
    {
        _configurator = configurator;
    }
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        return _configurator(builder);
    }
}
