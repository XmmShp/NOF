using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure.Core;

internal class DelegateApplicationInitializationStep : ApplicationInitializationStep, IBusinessLogicInitializationStep
{
    public DelegateApplicationInitializationStep(Func<IApplicationInitializationContext, IHost, Task> func) : base(func)
    {
    }
}
