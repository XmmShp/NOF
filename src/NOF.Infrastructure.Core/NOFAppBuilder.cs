using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Application;
using NOF.Contract;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NOF.Integration.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]

namespace NOF.Infrastructure.Core;

/// <summary>
/// Represents a configurable application host builder for the NOF framework,
/// providing a fluent API to customize service registration, application startup,
/// metadata, and integration with infrastructure.
/// </summary>
public interface INOFAppBuilder : IHostApplicationBuilder
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
    /// Registers an application configuration delegate that runs after the web application is built.
    /// Use this to configure middleware, endpoints, and other runtime pipeline components.
    /// </summary>
    /// <param name="initializationStep">The application configurator to register. Must not be null.</param>
    /// <returns>The current builder instance to allow method chaining.</returns>
    INOFAppBuilder AddInitializationStep(IApplicationInitializationStep initializationStep);

    /// <summary>
    /// Removes all previously registered application configurators that satisfy the given condition.
    /// </summary>
    /// <param name="predicate">A predicate used to identify which application configurators to remove.</param>
    /// <returns>The current builder instance to allow method chaining.</returns>
    INOFAppBuilder RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate);

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
    /// source-generated <c>AddAllHandlers</c> or manually via <see cref="NOFInfrastructureCoreExtensions.AddHandlerInfo"/>.
    /// </summary>
    HandlerInfos HandlerInfos => Services.GetOrAddSingleton<HandlerInfos>();

    /// <summary>
    /// Adds a service configuration delegate that will be executed during the service registration phase.
    /// The delegate receives the current application builder and can register services into the DI container.
    /// This overload supports asynchronous initialization via <see cref="ValueTask"/>.
    /// </summary>
    /// <param name="func">
    /// A delegate that configures services. It is wrapped in a <see cref="DelegateServiceRegistrationStep"/>
    /// and scheduled to run when the host application builds its service collection.
    /// </param>
    /// <returns>The same <see cref="INOFAppBuilder"/> instance for fluent chaining.</returns>
    INOFAppBuilder AddRegistrationStep(Func<INOFAppBuilder, ValueTask> func)
        => AddRegistrationStep(new DelegateServiceRegistrationStep(func));

    /// <summary>
    /// Removes all registered service configurations of the specified type <typeparamref name="T"/>.
    /// This allows dynamic customization or override of previously added configurations (e.g., in testing or modular composition).
    /// </summary>
    /// <typeparam name="T">
    /// The service configuration type to remove. Must derive from <see cref="IServiceRegistrationStep"/>.
    /// </typeparam>
    /// <returns>The same <see cref="INOFAppBuilder"/> instance for fluent chaining.</returns>
    INOFAppBuilder RemoveRegistrationStep<T>() where T : IServiceRegistrationStep
        => RemoveRegistrationStep(t => t is T);

    /// <summary>
    /// Adds an application configuration delegate that will be executed after the host is built but before it starts.
    /// The delegate receives the application builder and the constructed host application instance,
    /// allowing final adjustments (e.g., middleware pipeline, event subscriptions).
    /// This overload supports full asynchronous execution via <see cref="Task"/>.
    /// </summary>
    /// <param name="func">
    /// A delegate that performs post-build application configuration. It is wrapped in a
    /// <see cref="DelegateApplicationInitializationStep"/> and invoked during the application startup phase.
    /// </param>
    /// <returns>The same <see cref="INOFAppBuilder"/> instance for fluent chaining.</returns>
    INOFAppBuilder AddInitializationStep(Func<INOFAppBuilder, IHost, Task> func)
        => AddInitializationStep(new DelegateApplicationInitializationStep(func));

    /// <summary>
    /// Removes all registered application configurations of the specified type <typeparamref name="T"/>.
    /// Enables runtime adjustment of startup behavior, such as disabling a feature module during integration tests.
    /// </summary>
    /// <typeparam name="T">
    /// The application configuration type to remove. Must derive from <see cref="IApplicationInitializationStep"/>.
    /// </typeparam>
    /// <returns>The same <see cref="INOFAppBuilder"/> instance for fluent chaining.</returns>
    INOFAppBuilder RemoveInitializationStep<T>() where T : IApplicationInitializationStep
        => RemoveInitializationStep(t => t is T);
}

/// <summary>
/// Provides a base implementation of <see cref="INOFAppBuilder"/> that coordinates
/// the application construction lifecycle through modular, dependency-aware configuration units.
/// </summary>
/// <remarks>
/// <para>
/// This builder orchestrates two distinct phases:
/// <list type="bullet">
///   <item><description><b>Service Configuration Phase</b>: Executes all registered <see cref="IServiceRegistrationStep"/>
///   instances to populate the dependency injection container and configure infrastructure services.</description></item>
///   <item><description><b>Application Configuration Phase</b>: After the host application is built,
///   executes all registered <see cref="IApplicationInitializationStep"/> instances to perform
///   final setup such as middleware registration, event subscriptions, or background task initialization.</description></item>
/// </list>
/// </para>
/// <para>
/// Configuration units are executed in topological order based on declared dependencies
/// (via <see cref="IAfter{T}"/>), enabling safe, composable module composition.
/// </para>
/// <para>
/// Derived classes must implement <see cref="BuildApplicationAsync"/> to construct the concrete host using the configured service collection.
/// </para>
/// </remarks>
/// <typeparam name="THostApplication">
/// The concrete type of the host application being built. Must be a class implementing <see cref="IHost"/>.
/// </typeparam>
public abstract class NOFAppBuilder<THostApplication> : INOFAppBuilder
    where THostApplication : class, IHost
{
    /// <summary>
    /// A collection of service configuration units that will be executed during the DI registration phase.
    /// These configurations are typically added via <see cref="AddRegistrationStep"/> or delegate overloads,
    /// and are executed in dependency-aware order before the host application is constructed.
    /// </summary>
    protected readonly HashSet<IServiceRegistrationStep> ServiceConfigs;

    /// <summary>
    /// A collection of application configuration units that will be executed after the host application
    /// has been built, but before it starts running. These allow final setup such as middleware registration,
    /// event subscriptions, or background task initialization.
    /// Configurations are executed in dependency-resolved order via <see cref="AddInitializationStep"/>.
    /// </summary>
    protected readonly HashSet<IApplicationInitializationStep> ApplicationConfigs;

    /// <inheritdoc/>
    public IStartupEventChannel StartupEventChannel { get; protected set; }

    /// <inheritdoc/>
    public IRequestRider? RequestSender { get; set; }

    /// <inheritdoc/>
    public IEndpointNameProvider EndpointNameProvider { get; set; }


    /// <summary>
    /// Initializes a new instance of the <see cref="NOFAppBuilder{THostApplication}"/> class.
    /// Sets up a default implementation of <see cref="IStartupEventChannel"/> for internal configuration events
    /// and registers all default service registration steps.
    /// </summary>
    protected NOFAppBuilder()
    {
        StartupEventChannel = new StartupEventChannel();
        ServiceConfigs =
        [
            new CoreServicesRegistrationStep(),
            new CacheServiceRegistrationStep(),
            new OutboxRegistrationStep(),
            new OpenTelemetryRegistrationStep(),

            new ExceptionMiddlewareStep(),
            new InvocationContextMiddlewareStep(),
            new PermissionAuthorizationMiddlewareStep(),
            new ActivityTracingMiddlewareStep(),
            new AutoInstrumentationMiddlewareStep(),
            new MessageInboxMiddlewareStep(),
            new MessageOutboxMiddlewareStep(),
            
            // Default outbound middleware steps
            new MessageIdOutboundMiddlewareStep(),
            new TracingOutboundMiddlewareStep(),
            new AuthorizationOutboundMiddlewareStep(),
            new TenantOutboundMiddlewareStep(),
        ];
        EndpointNameProvider = new EndpointNameProvider();
        ApplicationConfigs = [];
    }

    /// <inheritdoc />
    public virtual INOFAppBuilder AddRegistrationStep(IServiceRegistrationStep registrationStep)
    {
        ArgumentNullException.ThrowIfNull(registrationStep);
        ServiceConfigs.Add(registrationStep);
        return this;
    }

    /// <inheritdoc />
    public virtual INOFAppBuilder AddInitializationStep(IApplicationInitializationStep initializationStep)
    {
        ArgumentNullException.ThrowIfNull(initializationStep);
        ApplicationConfigs.Add(initializationStep);
        return this;
    }

    /// <inheritdoc />
    public virtual INOFAppBuilder RemoveRegistrationStep(Predicate<IServiceRegistrationStep> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ServiceConfigs.RemoveWhere(predicate);
        return this;
    }

    /// <inheritdoc />
    public virtual INOFAppBuilder RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ApplicationConfigs.RemoveWhere(predicate);
        return this;
    }

    /// <summary>
    /// Asynchronously constructs and initializes the final host application instance.
    /// Executes all registered service registration steps in dependency order,
    /// builds the host, then executes all application initialization steps.
    /// </summary>
    /// <returns>A task that resolves to the fully configured host application.</returns>
    public virtual async Task<THostApplication> BuildAsync()
    {
        var regGraph = new ConfiguratorGraph<IServiceRegistrationStep>(ServiceConfigs);
        foreach (var task in regGraph.GetExecutionOrder())
        {
            await task.ExecuteAsync(this).ConfigureAwait(false);
        }

        var app = await BuildApplicationAsync();

        var startGraph = new ConfiguratorGraph<IApplicationInitializationStep>(ApplicationConfigs);
        foreach (var task in startGraph.GetExecutionOrder())
        {
            await task.ExecuteAsync(this, app).ConfigureAwait(false);
        }

        return app;
    }

    /// <summary>
    /// When overridden in a derived class, constructs the concrete host application, using the current service collection and configuration.
    /// This method is called after all service configurations have been applied and before application configurations run.
    /// </summary>
    /// <returns>A task that resolves to the built host application instance.</returns>
    protected abstract Task<THostApplication> BuildApplicationAsync();

    #region Abstractions
    /// <inheritdoc/>
    public abstract void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null) where TContainerBuilder : notnull;
    /// <inheritdoc/>
    public abstract IDictionary<object, object> Properties { get; }
    /// <inheritdoc/>
    public abstract IConfigurationManager Configuration { get; }
    /// <inheritdoc/>
    public abstract IHostEnvironment Environment { get; }
    /// <inheritdoc/>
    public abstract ILoggingBuilder Logging { get; }
    /// <inheritdoc/>
    public abstract IMetricsBuilder Metrics { get; }
    /// <inheritdoc/>
    public abstract IServiceCollection Services { get; }
    #endregion
}
