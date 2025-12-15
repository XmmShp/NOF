using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NOF;

// ReSharper disable once InconsistentNaming
public static partial class __NOF_Integration_Extensions__
{
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers a hosted service that executes an asynchronous delegate when the host starts.
        /// The delegate receives the <see cref="IServiceProvider"/> and a <see cref="CancellationToken"/>
        /// for graceful shutdown, and runs as a background task managed by the host lifetime.
        /// </summary>
        /// <param name="startAction">
        /// An asynchronous function invoked during the hosted service's <c>StartAsync</c> phase.
        /// It should honor the cancellation token and avoid blocking indefinitely.
        /// </param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection AddHostedService(Func<IServiceProvider, CancellationToken, Task> startAction)
        {
            return services.AddHostedService(sp => new DelegateHostedService(sp, startAction));
        }

        /// <summary>
        /// Registers a hosted service that executes a synchronous delegate when the host starts.
        /// The delegate receives the <see cref="IServiceProvider"/> and a <see cref="CancellationToken"/>,
        /// and is wrapped in a completed task to integrate with the async hosted service lifecycle.
        /// </summary>
        /// <param name="startAction">
        /// A synchronous action invoked during the hosted service's <c>StartAsync</c> phase.
        /// Although executed synchronously, it must still respect the cancellation token
        /// and complete promptly to avoid delaying application startup or shutdown.
        /// </param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection AddHostedService(Action<IServiceProvider, CancellationToken> startAction)
            => services.AddHostedService((sp, ct) => { startAction(sp, ct); return Task.CompletedTask; });

        /// <summary>
        /// Configures strongly-typed options using configuration binding, automatic section naming,
        /// data annotation validation, and startup-time validation.
        /// If no <paramref name="configSectionPath"/> is provided, the section name is inferred
        /// from the type name of <typeparamref name="TOptions"/> (e.g., "MyFeature" for MyFeatureOptions).
        /// </summary>
        /// <typeparam name="TOptions">The options type to configure. Must be a reference type.</typeparam>
        /// <param name="configSectionPath">
        /// Optional path to the configuration section. If <see langword="null"/> or empty,
        /// the section name is derived from <typeparamref name="TOptions"/> using convention.
        /// </param>
        /// <returns>An <see cref="OptionsBuilder{TOptions}"/> for further configuration chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the inferred configuration section does not exist or fails validation at startup.
        /// </exception>
        public OptionsBuilder<TOptions> AddOptionsInConfiguration<TOptions>(string? configSectionPath = null) where TOptions : class
        {
            // ReSharper disable once InvertIf
            if (string.IsNullOrEmpty(configSectionPath))
            {
                configSectionPath = string.GetSectionNameFromOptions<TOptions>();
            }

            return services.AddOptions<TOptions>()
                .BindConfiguration(configSectionPath)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }
    }
}

internal sealed class DelegateHostedService : BackgroundService
{
    private readonly Func<IServiceProvider, CancellationToken, Task> _startAction;
    private readonly IServiceProvider _serviceProvider;

    public DelegateHostedService(IServiceProvider serviceProvider, Func<IServiceProvider, CancellationToken, Task> startAction)
    {
        ArgumentNullException.ThrowIfNull(startAction);
        _serviceProvider = serviceProvider;
        _startAction = startAction;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _startAction(_serviceProvider, stoppingToken);
    }
}