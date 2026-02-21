using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Read-only context available during application initialization steps.
/// Provides access to services, configuration, and metadata but does not allow
/// adding or removing any steps.
/// </summary>
public interface IApplicationInitializationContext : IHostApplicationBuilder
{
    /// <summary>
    /// Gets the configuration-time event dispatcher used to enable plugin-style customization
    /// during application setup. This dispatcher allows modules to react to configuration lifecycle
    /// events without tight coupling. 
    /// </summary>
    IStartupEventChannel StartupEventChannel { get; }

    /// <summary>
    /// Gets or sets the request rider instance provided by the bus provider to dispatch
    /// application requests to their corresponding handlers at startup time.
    /// This property is typically set during service configuration.
    /// </summary>
    IRequestRider? RequestSender { get; set; }

    /// <summary>
    /// Gets or sets the endpoint name provider used for resolving message endpoint names.
    /// </summary>
    IEndpointNameProvider EndpointNameProvider { get; set; }

    /// <summary>
    /// Gets the set of handler metadata (e.g., command, event, request handlers) registered via
    /// source-generated <c>AddAllHandlers</c> or manually via <c>AddHandlerInfo</c>.
    /// </summary>
    HandlerInfos HandlerInfos { get; }
}

/// <summary>
/// Context available during service registration steps.
/// Extends <see cref="IApplicationInitializationContext"/> with the ability to schedule
/// initialization steps (which run later), but does NOT allow adding more registration steps
/// (the registration graph is already being executed).
/// </summary>
public interface IServiceRegistrationContext : IApplicationInitializationContext
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
}

/// <summary>
/// Represents a configurable application host builder for the NOF framework,
/// providing a fluent API to customize service registration, application startup,
/// metadata, and integration with infrastructure.
/// <para>
/// This is the full builder interface available before <c>BuildAsync</c> is called.
/// During step execution, narrower context interfaces are used to prevent invalid operations:
/// <list type="bullet">
///   <item><see cref="IServiceRegistrationContext"/> — used by <see cref="IServiceRegistrationStep"/>; cannot add registration steps.</item>
///   <item><see cref="IApplicationInitializationContext"/> — used by <see cref="IApplicationInitializationStep"/>; cannot add any steps.</item>
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
