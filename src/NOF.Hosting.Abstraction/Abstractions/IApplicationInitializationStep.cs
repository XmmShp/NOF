using Microsoft.Extensions.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

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
    /// The <paramref name="context"/> is an <see cref="IHostApplicationBuilder"/> which provides
    /// read-only access to services, configuration, and metadata but does NOT allow adding any steps.
    /// </para>
    /// </summary>
    /// <param name="context">The initialization context used for contextual information and services.</param>
    /// <param name="app">The constructed host application instance.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ExecuteAsync(IHostApplicationBuilder context, IHost app);
}

/// <summary>
/// CRTP variant of <see cref="IApplicationInitializationStep"/> that automatically provides the
/// <see cref="IStep.Type"/> implementation via <see cref="IStep{TSelf}"/>.
/// Concrete initialization steps should implement this interface to be fully AOT-compatible.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
public interface IApplicationInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : IStep<TSelf>, IApplicationInitializationStep
    where TSelf : IApplicationInitializationStep<TSelf>;
