using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NOF.Hosting;

/// <summary>
/// Provides a base implementation of <see cref="INOFAppBuilder"/> that coordinates
/// the application construction lifecycle through modular, dependency-aware configuration units.
/// </summary>
/// <remarks>
/// <para>
/// This builder finalizes host creation and then executes all registered
/// <see cref="IApplicationInitializationStep"/> instances to perform
/// post-build setup such as middleware registration, event subscriptions, or background task initialization.
/// </para>
/// <para>
/// Configuration units are executed in topological order based on declared dependencies
/// (via <see cref="ITopologizable{TContract}"/>), enabling safe, composable module composition.
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
    protected NOFAppBuilder()
    {
    }

    public virtual async Task<THostApplication> BuildAsync()
    {
        Services.TryAddSingleton(Environment);
        Services.AddNOFHosting();
        var app = await BuildApplicationAsync();

        var startGraph = new DependencyGraph<IApplicationInitializationStep>(
            app.Services.GetServices<IApplicationInitializationStep>());
        foreach (var task in startGraph.GetExecutionOrder())
        {
            await task.ExecuteAsync(app).ConfigureAwait(false);
        }

        return app;
    }

    protected abstract Task<THostApplication> BuildApplicationAsync();

    #region Abstractions
    public abstract void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null) where TContainerBuilder : notnull;
    public abstract IDictionary<object, object> Properties { get; }
    public abstract IConfigurationManager Configuration { get; }
    public abstract IHostEnvironment Environment { get; }
    public abstract ILoggingBuilder Logging { get; }
    public abstract IMetricsBuilder Metrics { get; }
    public abstract IServiceCollection Services { get; }
    #endregion
}
