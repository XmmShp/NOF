using Microsoft.Extensions.Hosting;

namespace NOF;

/// <summary>
/// Represents a marker interface for all configuration units in the application.
/// Serves as a base contract to identify types that contribute to the application's setup process.
/// </summary>
public interface IConfig;

/// <summary>
/// Indicates that a configuration type has an explicit dependency on another configuration type <typeparamref name="TDependency"/>.
/// This contract enables the framework to order configuration execution based on declared dependencies,
/// ensuring that <typeparamref name="TDependency"/> is executed before the implementing type.
/// </summary>
/// <typeparam name="TDependency">
/// The configuration type this component depends on. Must implement <see cref="IConfig"/>.
/// </typeparam>
public interface IAfter<TDependency> where TDependency : IConfig;

/// <summary>
/// Indicates that the implementing configurator must execute before any configurator.
/// This provides a way to declare ordering without modifying the dependent type.
/// </summary>
/// <typeparam name="TDependency">The configurator type that should run after this one.</typeparam>
public interface IBefore<TDependency> where TDependency : IConfig;

/// <summary>
/// Defines a service-level configuration unit that participates in the DI container registration phase.
/// Implementations are executed early in the application lifecycle, before the host is built,
/// and may register services, configure options, or set up infrastructure components.
/// </summary>
public interface IServiceConfig : IConfig
{
    /// <summary>
    /// Asynchronously executes the service configuration logic using the provided application builder.
    /// This method is called during the service registration stage and should not perform I/O-bound
    /// operations that block host startup unless necessary.
    /// </summary>
    /// <param name="builder">The application builder used to access services, configuration, and extension points.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask ExecuteAsync(INOFAppBuilder builder);
}

public class ServiceConfig : IServiceConfig
{
    private readonly Func<INOFAppBuilder, ValueTask> _configurator;

    public ServiceConfig(Func<INOFAppBuilder, ValueTask> configurator)
    {
        _configurator = configurator;
    }
    public ValueTask ExecuteAsync(INOFAppBuilder builder)
    {
        return _configurator(builder);
    }
}

/// <summary>
/// Defines an application-level configuration unit that runs after the host application has been fully constructed.
/// Implementations can interact with the live host instance (e.g., configure middleware pipelines,
/// subscribe to runtime events, or start background tasks).
/// </summary>
public interface IApplicationConfig : IConfig
{
    /// <summary>
    /// Asynchronously executes the application configuration logic using both the application builder
    /// and the fully built host application instance.
    /// This method is typically invoked just before the host starts, enabling final runtime customizations.
    /// </summary>
    /// <param name="builder">The application builder used for contextual information and services.</param>
    /// <param name="app">The constructed host application instance </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ExecuteAsync(INOFAppBuilder builder, IHost app);
}

public class ApplicationConfig : IApplicationConfig
{
    private readonly Func<INOFAppBuilder, IHost, Task> _configurator;

    public ApplicationConfig(Func<INOFAppBuilder, IHost, Task> configurator)
    {
        _configurator = configurator;
    }

    public Task ExecuteAsync(INOFAppBuilder builder, IHost app)
    {
        return _configurator(builder, app);
    }
}