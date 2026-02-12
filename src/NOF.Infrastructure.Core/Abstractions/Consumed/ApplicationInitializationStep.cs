using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Defines an application-level configuration unit that runs after the host application has been fully constructed.
/// Implementations can interact with the live host instance (e.g., configure middleware pipelines,
/// subscribe to runtime events, or start background tasks).
/// </summary>
public interface IApplicationInitializationStep : IStep
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

public class ApplicationInitializationStep : IApplicationInitializationStep
{
    private readonly Func<INOFAppBuilder, IHost, Task> _configurator;

    public ApplicationInitializationStep(Func<INOFAppBuilder, IHost, Task> configurator)
    {
        _configurator = configurator;
    }

    public Task ExecuteAsync(INOFAppBuilder builder, IHost app)
    {
        return _configurator(builder, app);
    }
}
