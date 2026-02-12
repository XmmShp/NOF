namespace NOF.Infrastructure.Core;

internal class DelegateServiceRegistrationStep : ServiceRegistrationStep, IDependentServiceRegistrationStep
{
    public DelegateServiceRegistrationStep(Func<INOFAppBuilder, ValueTask> func) : base(func)
    {
    }
}
