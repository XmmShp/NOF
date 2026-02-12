using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure.Core;

internal class DelegateApplicationInitializationStep : ApplicationInitializationStep, IBusinessLogicInitializationStep
{
    public DelegateApplicationInitializationStep(Func<INOFAppBuilder, IHost, Task> func) : base(func)
    {
    }
}
