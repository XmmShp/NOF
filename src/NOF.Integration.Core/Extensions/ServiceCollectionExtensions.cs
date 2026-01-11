using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        /// Adds a memory-based cache service for development and testing.
        /// </summary>
        /// <param name="configureOptions">Optional action to configure the cache service options.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public IServiceCollection AddMemoryCache(Action<CacheServiceOptions>? configureOptions = null)
        {
            var options = new CacheServiceOptions();
            configureOptions?.Invoke(options);

            return services.AddCacheService<MemoryCacheService>((sp, opt) =>
            {
                var serializer = opt.GetSerializer(sp);
                var lockRetryStrategy = opt.GetLockRetryStrategy(sp);
                return new MemoryCacheService(serializer, lockRetryStrategy, opt);
            }, options);
        }

        /// <summary>
        /// Adds a cache service implementation with automatic interface registration.
        /// </summary>
        /// <typeparam name="TImplementation">The cache service implementation type.</typeparam>
        /// <param name="implementationFactory">Factory to create the cache service instance.</param>
        /// <param name="options">The cache service options.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public IServiceCollection AddCacheService<TImplementation>(Func<IServiceProvider, CacheServiceOptions, TImplementation> implementationFactory,
            CacheServiceOptions options)
            where TImplementation : class, ICacheServiceWithRawAccess
        {
            services.AddSingleton(sp => implementationFactory(sp, options));

            // Register all cache-related interfaces
            services.TryAddSingleton<IDistributedCache>(sp => sp.GetRequiredService<TImplementation>());
            services.TryAddSingleton<ICacheService>(sp => sp.GetRequiredService<TImplementation>());
            services.TryAddSingleton<ICacheServiceWithRawAccess>(sp => sp.GetRequiredService<TImplementation>());

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