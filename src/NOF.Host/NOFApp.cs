using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NOF.Integration.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]

namespace NOF;

/// <summary>
/// Default implementation of <see cref="INOFApp"/>, providing a fluent API
/// to configure and build an ASP.NET Core application using the NOF framework.
/// </summary>
public class NOFApp : INOFApp
{
    internal readonly HashSet<IRegistrationConfigurator> RegistrationStages = [];
    internal readonly HashSet<IStartupConfigurator> StartupStages = [];
    private readonly WebApplicationBuilder _builder;

    /// <inheritdoc />
    public INOFAppMetadata Metadata { get; }

    /// <inheritdoc />
    public IServiceCollection Services => _builder.Services;

    /// <inheritdoc />
    public INOFApp AddRegistrationConfigurator(IRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        RegistrationStages.Add(configurator);
        return this;
    }

    /// <inheritdoc />
    public INOFApp AddStartupConfigurator(IStartupConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        StartupStages.Add(configurator);
        return this;
    }

    /// <inheritdoc />
    public INOFApp RemoveRegistrationConfigurator(Predicate<IRegistrationConfigurator> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        RegistrationStages.RemoveWhere(predicate);
        return this;
    }

    /// <inheritdoc />
    public INOFApp RemoveStartupConfigurator(Predicate<IStartupConfigurator> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        StartupStages.RemoveWhere(predicate);
        return this;
    }

    /// <inheritdoc />
    public ICommandSender? CommandSender { get; set; }

    /// <inheritdoc />
    public WebApplicationBuilder Unwrap() => _builder;

    /// <summary>
    /// Builds the underlying ASP.NET Core application by:<br/>
    /// 1. Executing all registration configurators (to populate the service container),<br/>
    /// 2. Building the <see cref="WebApplication"/>,<br/>
    /// 3. Executing all startup configurators (to configure middleware and endpoints).
    /// </summary>
    /// <returns>A fully configured <see cref="WebApplication"/> ready to run.</returns>
    public async Task<WebApplication> BuildAsync()
    {
        // Execute registration phase in dependency order
        var regGraph = new ConfiguratorGraph<IRegistrationConfigurator>(RegistrationStages);
        foreach (var task in regGraph.GetExecutionOrder())
        {
            await task.ExecuteAsync(this).ConfigureAwait(false);
        }

        // Build the web application
        var app = _builder.Build();

        // Execute startup phase in dependency order
        var startGraph = new ConfiguratorGraph<IStartupConfigurator>(StartupStages);
        foreach (var task in startGraph.GetExecutionOrder())
        {
            await task.ExecuteAsync(this, app).ConfigureAwait(false);
        }

        return app;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="NOFApp"/> with the specified command-line arguments.
    /// Internally creates a <see cref="WebApplicationBuilder"/> using <see cref="WebApplication.CreateBuilder(string[])"/>.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the application.</param>
    private NOFApp(string[] args)
    {
        _builder = WebApplication.CreateBuilder(args);
        Metadata = new NOFAppMetadata();
    }

    /// <summary>
    /// Creates a new instance of <see cref="NOFApp"/> using the provided command-line arguments.
    /// This is the entry point for configuring a NOF-based application.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>A new <see cref="NOFApp"/> instance.</returns>
    public static NOFApp Create(string[] args)
    {
        var app = new NOFApp(args);
        app.Services.AddScoped<ISender, Sender>();
        app.Services.AddScoped<IPublisher, Publisher>();
        return app;
    }
}