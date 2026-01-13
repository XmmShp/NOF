using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace NOF;

/// <summary />
// ReSharper disable once InconsistentNaming
public static partial class __NOF_Integration_Extensions__
{
    private const string Assemblies = "NOF.Integration.Core:Assemblies";
    private const string ExtraHandlerInfos = "NOF.Integration.Core:ExtraHandlerInfos";
    private const string ActivitySources = "NOF.Integration.Core:ActivitySources";
    private const string MetricNames = "NOF.Integration.Core:MetricNames";

    /// <param name="builder">The <see cref="INOFAppBuilder{THostApplication}"/> to operate on.</param>
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Gets the list of assemblies registered for scanning (e.g., for handlers, validators, or configuration types).
        /// This collection is lazily initialized and shared across the application builder's lifetime.
        /// Extensions or modules can add their assemblies here to enable convention-based discovery during startup.
        /// </summary>
        public HashSet<Assembly> Assemblies => builder.Properties.GetOrAdd(Assemblies, _ => new HashSet<Assembly>());

        public List<string> ActivitySources => builder.Properties.GetOrAdd(ActivitySources, _ => new List<string>());

        public List<string> MetricNames => builder.Properties.GetOrAdd(MetricNames, _ => new List<string>());

        /// <summary>
        /// Registers the assembly containing the specified type as an application part for HTTP endpoint discovery.
        /// This enables the framework to scan the assembly for request types marked with <see cref="ExposeToHttpEndpointAttribute"/>.
        /// </summary>
        /// <typeparam name="T">A type whose containing assembly will be added.</typeparam>
        /// <returns>The current <see cref="INOFAppBuilder"/> instance.</returns>
        public INOFAppBuilder WithApplicationPart<T>()
        {
            builder.WithApplicationPart(typeof(T).Assembly);
            return builder;
        }

        /// <summary>
        /// Registers one or more assemblies as application parts for HTTP endpoint discovery.
        /// The framework will scan these assemblies for request types marked with <see cref="ExposeToHttpEndpointAttribute"/>.
        /// </summary>
        /// <param name="assemblies">The assemblies to include in endpoint scanning.</param>
        /// <returns>The current <see cref="INOFAppBuilder"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="assemblies"/> is null.</exception>
        public INOFAppBuilder WithApplicationPart(params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(assemblies);

            foreach (var assembly in assemblies)
            {
                builder.Assemblies.Add(assembly);
            }
            return builder;
        }

        /// <summary>
        /// Gets the list of handler metadata (e.g., command, event, request handlers) discovered in the assemblies 
        /// configured for this builder. The scan is performed on-demand and results are cached per assembly.
        /// </summary>
        public IReadOnlyList<HandlerInfo> HandlerInfos
            => HandlerScanner.ScanHandlers(builder.Assemblies)
                .Union(builder.ExtraHandlerInfos)
                .ToList()
                .AsReadOnly();

        public List<HandlerInfo> ExtraHandlerInfos => builder.Properties.GetOrAdd(ExtraHandlerInfos, _ => new List<HandlerInfo>());

        /// <summary>
        /// Adds a service configuration delegate that will be executed during the service registration phase.
        /// The delegate receives the current application builder and can register services into the DI container.
        /// This overload supports asynchronous initialization via <see cref="ValueTask"/>.
        /// </summary>
        /// <param name="func">
        /// A delegate that configures services. It is wrapped in a <see cref="DelegateServiceRegistrationStep"/>
        /// and scheduled to run when the host application builds its service collection.
        /// </param>
        /// <returns>The same <see cref="INOFAppBuilder{THostApplication}"/> instance for fluent chaining.</returns>
        public INOFAppBuilder AddRegistrationStep(Func<INOFAppBuilder, ValueTask> func)
            => builder.AddRegistrationStep(new DelegateServiceRegistrationStep(func));

        /// <summary>
        /// Removes all registered service configurations of the specified type <typeparamref name="T"/>.
        /// This allows dynamic customization or override of previously added configurations (e.g., in testing or modular composition).
        /// </summary>
        /// <typeparam name="T">
        /// The service configuration type to remove. Must derive from <see cref="IServiceRegistrationStep"/>.
        /// </typeparam>
        /// <returns>The same <see cref="INOFAppBuilder{THostApplication}"/> instance for fluent chaining.</returns>
        public INOFAppBuilder RemoveRegistrationStep<T>() where T : IServiceRegistrationStep
            => builder.RemoveRegistrationStep(t => t is T);

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
        /// <returns>The same <see cref="INOFAppBuilder{THostApplication}"/> instance for fluent chaining.</returns>
        public INOFAppBuilder AddInitializationStep(Func<INOFAppBuilder, IHost, Task> func)
            => builder.AddInitializationStep(new DelegateApplicationInitializationStep(func));

        /// <summary>
        /// Removes all registered application configurations of the specified type <typeparamref name="T"/>.
        /// Enables runtime adjustment of startup behavior, such as disabling a feature module during integration tests.
        /// </summary>
        /// <typeparam name="T">
        /// The application configuration type to remove. Must derive from <see cref="IApplicationInitializationStep"/>.
        /// </typeparam>
        /// <returns>The same <see cref="INOFAppBuilder{THostApplication}"/> instance for fluent chaining.</returns>
        public INOFAppBuilder RemoveInitializationStep<T>() where T : IApplicationInitializationStep
            => builder.RemoveInitializationStep(t => t is T);
    }
}

internal class DelegateApplicationInitializationStep : ApplicationInitializationStep, IBusinessLogicInitializationStep
{
    public DelegateApplicationInitializationStep(Func<INOFAppBuilder, IHost, Task> func) : base(func)
    {
    }
}

internal class DelegateServiceRegistrationStep : ServiceRegistrationStep, IDependentServiceRegistrationStep
{
    public DelegateServiceRegistrationStep(Func<INOFAppBuilder, ValueTask> func) : base(func)
    {
    }
}