using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Context available during service registration steps.
/// Extends <see cref="IHostApplicationBuilder"/> with the ability to schedule
/// initialization steps (which run later), but does NOT allow adding more registration steps
/// (the registration graph is already being executed).
/// </summary>
public interface IServiceRegistrationContext : IHostApplicationBuilder
{
    /// <summary>
    /// Registers an application configuration delegate that runs after the web application is built.
    /// Use this to configure middleware, endpoints, and other runtime pipeline components.
    /// </summary>
    /// <param name="initializationStep">The application configurator to register. Must not be null.</param>
    /// <returns>The current builder instance to allow method chaining.</returns>
    IServiceRegistrationContext AddInitializationStep(IApplicationInitializationStep initializationStep);

    /// <summary>
    /// Removes all previously registered application configurators that satisfy the given condition.
    /// </summary>
    /// <param name="predicate">A predicate used to identify which application configurators to remove.</param>
    /// <returns>The current builder instance to allow method chaining.</returns>
    IServiceRegistrationContext RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate);

    /// <summary>
    /// Removes all registered application configurations of the specified type <typeparamref name="T"/>.
    /// </summary>
    IServiceRegistrationContext RemoveInitializationStep<T>() where T : IApplicationInitializationStep
        => RemoveInitializationStep(t => t is T);

    /// <summary>
    /// Adds the initialization step only if a step of the same runtime type has not been registered.
    /// </summary>
    IServiceRegistrationContext TryAddInitializationStep(IApplicationInitializationStep initializationStep)
    {
        ArgumentNullException.ThrowIfNull(initializationStep);
        var exists = false;
        RemoveInitializationStep(existing =>
        {
            if (existing.GetType() == initializationStep.GetType())
            {
                exists = true;
            }
            return false;
        });

        if (exists)
        {
            return this;
        }

        return AddInitializationStep(initializationStep);
    }

    /// <summary>
    /// Adds the initialization step type only if it has not been registered.
    /// </summary>
    IServiceRegistrationContext TryAddInitializationStep<T>() where T : IApplicationInitializationStep, new()
        => TryAddInitializationStep(new T());
}
