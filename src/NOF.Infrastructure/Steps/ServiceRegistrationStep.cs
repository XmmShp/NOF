using NOF.Hosting;

namespace NOF.Infrastructure;

public class ServiceRegistrationStep : IServiceRegistrationStep
{
    public TopologyComparison Compare(IServiceRegistrationStep other) => TopologyComparison.DoesNotMatter;

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
