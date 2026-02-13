namespace NOF.Infrastructure.Core;

internal class DelegateServiceRegistrationStep : ServiceRegistrationStep, IDependentServiceRegistrationStep
{
    public DelegateServiceRegistrationStep(Func<IServiceRegistrationContext, ValueTask> func) : base(func)
    {
    }
}
