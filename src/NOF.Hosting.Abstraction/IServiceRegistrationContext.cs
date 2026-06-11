using Microsoft.Extensions.Hosting;
using NOF.Abstraction;

namespace NOF.Hosting;

/// <summary>
/// Context available during service registration steps.
/// </summary>
public interface IServiceRegistrationContext : IHostApplicationBuilder
{
    Registry Registry { get; }

    IServiceRegistrationContext AddInitializationStep(IApplicationInitializationStep initializationStep);

    IServiceRegistrationContext RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate);
}

public static partial class NOFHostingExtensions
{
    extension(IServiceRegistrationContext context)
    {
        public IServiceRegistrationContext RemoveInitializationStep<T>() where T : IApplicationInitializationStep
            => context.RemoveInitializationStep(t => t is T);

        public IServiceRegistrationContext TryAddInitializationStep(IApplicationInitializationStep initializationStep)
        {
            ArgumentNullException.ThrowIfNull(initializationStep);
            var exists = false;
            context.RemoveInitializationStep(existing =>
            {
                if (existing.GetType() == initializationStep.GetType())
                {
                    exists = true;
                }
                return false;
            });

            if (exists)
            {
                return context;
            }

            return context.AddInitializationStep(initializationStep);
        }

        public IServiceRegistrationContext TryAddInitializationStep<T>() where T : IApplicationInitializationStep, new()
            => context.TryAddInitializationStep(new T());
    }
}
