using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Represents a configurable application host builder for the NOF framework,
/// providing a fluent API to customize service registration, application startup,
/// metadata, and integration with infrastructure.
/// <para>
/// This is the full builder interface available before <c>BuildAsync</c> is called.
/// During step execution, narrower context interfaces are used to prevent invalid operations:
/// <list type="bullet">
///   <item><see cref="IServiceRegistrationContext"/> used by <see cref="IServiceRegistrationStep"/>; cannot add registration steps.</item>
///   <item><see cref="IHostApplicationBuilder"/> used by <see cref="IApplicationInitializationStep"/>; cannot add any steps.</item>
/// </list>
/// </para>
/// </summary>
public interface INOFAppBuilder : IServiceRegistrationContext
{
    /// <summary>
    /// Registers a service configuration delegate that runs during DI container setup.
    /// Use this to add services required by your application or modules.
    /// </summary>
    /// <param name="registrationStep">The service configurator to register. Must not be null.</param>
    /// <returns>The current builder instance to allow method chaining.</returns>
    INOFAppBuilder AddRegistrationStep(IServiceRegistrationStep registrationStep);

    /// <summary>
    /// Removes all previously registered service configurators that satisfy the given condition.
    /// </summary>
    /// <param name="predicate">A predicate used to identify which service configurators to remove.</param>
    /// <returns>The current builder instance to allow method chaining.</returns>
    INOFAppBuilder RemoveRegistrationStep(Predicate<IServiceRegistrationStep> predicate);

    /// <summary>
    /// Removes all registered service configurations of the specified type <typeparamref name="T"/>.
    /// </summary>
    INOFAppBuilder RemoveRegistrationStep<T>() where T : IServiceRegistrationStep
        => RemoveRegistrationStep(t => t is T);

    IServiceRegistrationContext IServiceRegistrationContext.AddInitializationStep(IApplicationInitializationStep initializationStep)
        => AddInitializationStep(initializationStep);

    IServiceRegistrationContext IServiceRegistrationContext.RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
        => RemoveInitializationStep(predicate);

    /// <inheritdoc cref="IServiceRegistrationContext.AddInitializationStep(IApplicationInitializationStep)"/>
    new INOFAppBuilder AddInitializationStep(IApplicationInitializationStep initializationStep);

    /// <inheritdoc cref="IServiceRegistrationContext.RemoveInitializationStep(Predicate{IApplicationInitializationStep})"/>
    new INOFAppBuilder RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate);

    /// <summary>
    /// Removes all registered application configurations of the specified type <typeparamref name="T"/>.
    /// </summary>
    new INOFAppBuilder RemoveInitializationStep<T>() where T : IApplicationInitializationStep
        => RemoveInitializationStep(t => t is T);
}
