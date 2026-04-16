using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Hosting;

/// <summary>
/// Provides a base implementation of <see cref="INOFAppBuilder"/> that coordinates
/// the application construction lifecycle through modular, dependency-aware configuration units.
/// </summary>
/// <remarks>
/// <para>
/// This builder orchestrates two distinct phases:
/// <list type="bullet">
///   <item><description><b>Service Configuration Phase</b>: Executes all registered <see cref="IServiceRegistrationStep"/>
///   instances to populate the dependency injection container and configure host capabilities.</description></item>
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
    protected readonly HashSet<DependencyNode> ServiceConfigs = [];

    protected readonly HashSet<DependencyNode> ApplicationConfigs = [];

    protected NOFAppBuilder()
    {
        if (Assembly.GetEntryAssembly() is { } assembly)
        {
            this.AddApplicationPart(assembly);
        }
    }

    public virtual INOFAppBuilder AddRegistrationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep registrationStep, params Type[] allInterfaces)
        where TStep : IServiceRegistrationStep
    {
        ArgumentNullException.ThrowIfNull(registrationStep);
        ArgumentNullException.ThrowIfNull(allInterfaces);

        var interfaces = allInterfaces.Length == 0
            ? typeof(TStep).GetAllAssignableTypes()
            : allInterfaces;
        ServiceConfigs.Add(new DependencyNode(registrationStep, interfaces));
        return this;
    }

    public virtual INOFAppBuilder RemoveRegistrationStep(Predicate<IServiceRegistrationStep> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ServiceConfigs.RemoveWhere(node => predicate((IServiceRegistrationStep)node.ExtraInfo));
        return this;
    }

    public virtual INOFAppBuilder AddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep initializationStep, params Type[] allInterfaces)
        where TStep : IApplicationInitializationStep
    {
        ArgumentNullException.ThrowIfNull(initializationStep);
        ArgumentNullException.ThrowIfNull(allInterfaces);

        var interfaces = allInterfaces.Length == 0
            ? typeof(TStep).GetAllAssignableTypes()
            : allInterfaces;
        ApplicationConfigs.Add(new DependencyNode(initializationStep, interfaces));
        return this;
    }

    public virtual INOFAppBuilder RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ApplicationConfigs.RemoveWhere(node => predicate((IApplicationInitializationStep)node.ExtraInfo));
        return this;
    }

    IServiceRegistrationContext IServiceRegistrationContext.AddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep initializationStep, params Type[] allInterfaces)
        => AddInitializationStep(initializationStep, allInterfaces);

    IServiceRegistrationContext IServiceRegistrationContext.RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
        => RemoveInitializationStep(predicate);

    public virtual async Task<THostApplication> BuildAsync()
    {
        ConfigureContainer(new NOFServiceProviderFactory());
        AddRegistrationStep(new AutoInjectServiceRegistrationStep());
        this.AddHostingDefaults();
        var regGraph = new DependencyGraph<IServiceRegistrationStep>(ServiceConfigs);
        foreach (var node in regGraph.GetExecutionOrder())
        {
            var task = (IServiceRegistrationStep)node.ExtraInfo;
            await task.ExecuteAsync(this).ConfigureAwait(false);
        }

        var app = await BuildApplicationAsync();

        var startGraph = new DependencyGraph<IApplicationInitializationStep>(ApplicationConfigs);
        foreach (var node in startGraph.GetExecutionOrder())
        {
            var task = (IApplicationInitializationStep)node.ExtraInfo;
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
