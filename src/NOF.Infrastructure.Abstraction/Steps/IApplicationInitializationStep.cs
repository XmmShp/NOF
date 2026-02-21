using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Defines an application-level configuration unit that runs after the host application has been fully constructed.
/// Implementations can interact with the live host instance (e.g., configure middleware pipelines,
/// subscribe to runtime events, or start background tasks).
/// </summary>
public interface IApplicationInitializationStep : IStep
{
    /// <summary>
    /// Asynchronously executes the application configuration logic using both the initialization context
    /// and the fully built host application instance.
    /// This method is typically invoked just before the host starts, enabling final runtime customizations.
    /// <para>
    /// The <paramref name="context"/> is an <see cref="IApplicationInitializationContext"/> which provides
    /// read-only access to services, configuration, and metadata but does NOT allow adding any steps.
    /// </para>
    /// </summary>
    /// <param name="context">The initialization context used for contextual information and services.</param>
    /// <param name="app">The constructed host application instance.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ExecuteAsync(IApplicationInitializationContext context, IHost app);
}
