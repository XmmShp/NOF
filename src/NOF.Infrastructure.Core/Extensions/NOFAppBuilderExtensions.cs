using Microsoft.Extensions.Hosting;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

public static partial class NOFInfrastructureCoreExtensions
{
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Adds a service configuration delegate that will be executed during the service registration phase.
        /// </summary>
        public INOFAppBuilder AddRegistrationStep(Func<IServiceRegistrationContext, ValueTask> func)
            => builder.AddRegistrationStep(new ServiceRegistrationStep(func));

        /// <summary>
        /// Adds an application configuration delegate that will be executed after the host is built but before it starts.
        /// </summary>
        public INOFAppBuilder AddInitializationStep(Func<IHostApplicationBuilder, IHost, Task> func)
            => builder.AddInitializationStep(new ApplicationInitializationStep(func));
    }
}
