using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NOF;

// ReSharper disable once InconsistentNaming
public static partial class NOFInfrastructureCoreExtensions
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

        /// <summary>
        /// Replaces an existing service descriptor or adds a new one if it doesn't exist.
        /// </summary>
        /// <param name="descriptor">The service descriptor to replace or add.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection ReplaceOrAdd(ServiceDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            var existingDescriptor = services.FirstOrDefault(d => d.Lifetime == descriptor.Lifetime && d.ServiceType == descriptor.ServiceType);
            if (existingDescriptor is not null)
            {
                services.Remove(existingDescriptor);
            }

            services.Add(descriptor);
            return services;
        }

        /// <summary>
        /// Replaces an existing singleton service or adds a new one if it doesn't exist.
        /// </summary>
        /// <typeparam name="TService">The type of the service to register.</typeparam>
        /// <typeparam name="TImplementation">The implementation type of the service.</typeparam>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection ReplaceOrAddSingleton<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            return services.ReplaceOrAdd(ServiceDescriptor.Singleton<TService, TImplementation>());
        }

        /// <summary>
        /// Replaces an existing singleton service or adds a new one if it doesn't exist.
        /// </summary>
        /// <typeparam name="TService">The type of the service to register.</typeparam>
        /// <param name="implementationFactory">The factory that creates the service.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection ReplaceOrAddSingleton<TService>(Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(implementationFactory);
            return services.ReplaceOrAdd(ServiceDescriptor.Singleton(implementationFactory));
        }

        /// <summary>
        /// Replaces an existing singleton service or adds a new one if it doesn't exist.
        /// </summary>
        /// <typeparam name="TService">The type of the service to register.</typeparam>
        /// <param name="implementationInstance">The instance of the service.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection ReplaceOrAddSingleton<TService>(TService implementationInstance)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(implementationInstance);
            return services.ReplaceOrAdd(ServiceDescriptor.Singleton(implementationInstance));
        }

        /// <summary>
        /// Replaces an existing scoped service or adds a new one if it doesn't exist.
        /// </summary>
        /// <typeparam name="TService">The type of the service to register.</typeparam>
        /// <typeparam name="TImplementation">The implementation type of the service.</typeparam>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection ReplaceOrAddScoped<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            return services.ReplaceOrAdd(ServiceDescriptor.Scoped<TService, TImplementation>());
        }

        /// <summary>
        /// Replaces an existing scoped service or adds a new one if it doesn't exist.
        /// </summary>
        /// <typeparam name="TService">The type of the service to register.</typeparam>
        /// <param name="implementationFactory">The factory that creates the service.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection ReplaceOrAddScoped<TService>(Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(implementationFactory);
            return services.ReplaceOrAdd(ServiceDescriptor.Scoped(implementationFactory));
        }

        /// <summary>
        /// Replaces an existing transient service or adds a new one if it doesn't exist.
        /// </summary>
        /// <typeparam name="TService">The type of the service to register.</typeparam>
        /// <typeparam name="TImplementation">The implementation type of the service.</typeparam>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection ReplaceOrAddTransient<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            return services.ReplaceOrAdd(ServiceDescriptor.Transient<TService, TImplementation>());
        }

        /// <summary>
        /// Replaces an existing transient service or adds a new one if it doesn't exist.
        /// </summary>
        /// <typeparam name="TService">The type of the service to register.</typeparam>
        /// <param name="implementationFactory">The factory that creates the service.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection ReplaceOrAddTransient<TService>(Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(implementationFactory);
            return services.ReplaceOrAdd(ServiceDescriptor.Transient(implementationFactory));
        }

        /// <summary>
        /// Adds a cache service implementation with automatic interface registration.
        /// Supports multi-tenant architecture by creating tenant-specific cache instances with isolated key prefixes.
        /// </summary>
        /// <typeparam name="TImplementation">The cache service implementation type.</typeparam>
        /// <param name="optionsConfigurator">The cache service options.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public IServiceCollection ReplaceOrAddCacheService<TImplementation>(Action<CacheServiceOptions>? optionsConfigurator = null)
            where TImplementation : class, ICacheService
        {
            services.AddOptions<CacheServiceOptions>();
            if (optionsConfigurator is not null)
            {
                services.Configure(optionsConfigurator);
            }

            // Register as Scoped to support multi-tenant isolation
            services.ReplaceOrAddScoped(sp => sp.GetRequiredService<IOptions<CacheServiceOptions>>().Value.GetSerializer(sp));
            services.ReplaceOrAddScoped(sp => sp.GetRequiredService<IOptions<CacheServiceOptions>>().Value.GetLockRetryStrategy(sp));

            // Register multi-tenant aware cache service factory
            services.ReplaceOrAddScoped<ICacheService>(sp =>
            {
                var invocationContext = sp.GetRequiredService<IInvocationContext>();
                var baseOptions = sp.GetRequiredService<IOptions<CacheServiceOptions>>().Value;

                // Create tenant-specific options
                var tenantOptions = new CacheServiceOptions
                {
                    Serializer = baseOptions.Serializer,
                    SerializerFactory = baseOptions.SerializerFactory,
                    DefaultEntryOptions = baseOptions.DefaultEntryOptions,
                    MinimumLockRenewalDuration = baseOptions.MinimumLockRenewalDuration,
                    LockRenewalIntervalFactor = baseOptions.LockRenewalIntervalFactor,
                    LockRetryStrategy = baseOptions.LockRetryStrategy,
                    LockRetryStrategyFactory = baseOptions.LockRetryStrategyFactory,
                    Properties = baseOptions.Properties
                };

                // Set tenant-specific KeyPrefix for transparent multi-tenant isolation
                var tenantId = invocationContext.TenantId;
                if (!string.IsNullOrEmpty(tenantId))
                {
                    tenantOptions.KeyPrefix = string.IsNullOrEmpty(baseOptions.KeyPrefix)
                        ? $"tenant:{tenantId}:"
                        : $"{baseOptions.KeyPrefix}:tenant:{tenantId}:";
                }
                else
                {
                    tenantOptions.KeyPrefix = baseOptions.KeyPrefix;
                }

                var options = Options.Create(tenantOptions);

                return ActivatorUtilities.CreateInstance<TImplementation>(sp, options);
            });

            services.ReplaceOrAddScoped<IDistributedCache>(sp => sp.GetRequiredService<ICacheService>());
            return services;
        }

        /// <summary>
        /// Configures handler pipeline middleware (can be called multiple times).
        /// User-defined middleware is inserted between core middleware:
        /// Activity tracing -> Auto-instrumentation -> [User middleware] -> Transactional message context
        /// </summary>
        public IServiceCollection ConfigureHandlerPipeline(Action<IHandlerPipelineBuilder, IServiceProvider> configure)
        {
            // Get or create the configuration action list
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(List<Action<IHandlerPipelineBuilder, IServiceProvider>>));
            if (descriptor?.ImplementationInstance is List<Action<IHandlerPipelineBuilder, IServiceProvider>> actions)
            {
                actions.Add(configure);
            }

            return services;
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
