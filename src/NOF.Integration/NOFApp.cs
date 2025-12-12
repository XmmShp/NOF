using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NOF.Integration.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]

namespace NOF;

/// <summary>
/// Represents a configurable application host for the NOF framework,
/// encapsulating service registration, startup configuration, metadata,
/// and integration with the underlying ASP.NET Core infrastructure.
/// </summary>
public interface INOFApp
{
    /// <summary>
    /// Adds a registration configurator that will be executed during the service registration phase.
    /// Registration configurators are used to register services into the DI container.
    /// </summary>
    /// <param name="configurator">The configurator to add. Must not be null.</param>
    /// <returns>The current <see cref="INOFApp"/> instance for method chaining.</returns>
    INOFApp AddRegistrationConfigurator(IRegistrationConfigurator configurator);

    /// <summary>
    /// Adds a startup configurator that will be executed after the <see cref="WebApplication"/> is built,
    /// during the pipeline configuration phase (e.g., middleware, endpoints).
    /// </summary>
    /// <param name="configurator">The configurator to add. Must not be null.</param>
    /// <returns>The current <see cref="INOFApp"/> instance for method chaining.</returns>
    INOFApp AddStartupConfigurator(IStartupConfigurator configurator);

    /// <summary>
    /// Removes all registration configurators that match the specified predicate.
    /// </summary>
    /// <param name="predicate">A function to test each configurator for removal.</param>
    /// <returns>The current <see cref="INOFApp"/> instance for method chaining.</returns>
    INOFApp RemoveRegistrationConfigurator(Predicate<IRegistrationConfigurator> predicate);

    /// <summary>
    /// Removes all startup configurators that match the specified predicate.
    /// </summary>
    /// <param name="predicate">A function to test each configurator for removal.</param>
    /// <returns>The current <see cref="INOFApp"/> instance for method chaining.</returns>
    INOFApp RemoveStartupConfigurator(Predicate<IStartupConfigurator> predicate);

    /// <summary>
    /// Gets the underlying <see cref="IServiceCollection"/> used by the application.
    /// This allows direct service registration if needed, though using <see cref="IRegistrationConfigurator"/>
    /// is preferred for modularity.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Gets or sets the default command sender used by the application for dispatching commands.
    /// This is typically resolved from the DI container after service registration completes.
    /// </summary>
    ICommandSender? CommandSender { get; set; }

    /// <summary>
    /// Gets the metadata container associated with this application instance.
    /// Used to pass contextual information between configurators or during startup.
    /// </summary>
    INOFAppMetadata Metadata { get; }

    /// <summary>
    /// Unwraps and returns the underlying <see cref="WebApplicationBuilder"/>.
    /// Use this only when direct access to ASP.NET Core builder features is absolutely necessary.
    /// Prefer using configurators for extensibility and testability.
    /// </summary>
    /// <returns>The raw <see cref="WebApplicationBuilder"/> instance.</returns>
    WebApplicationBuilder Unwrap();
}