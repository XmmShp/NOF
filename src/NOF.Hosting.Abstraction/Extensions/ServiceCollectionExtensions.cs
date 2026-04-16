using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Configures strongly-typed options using configuration binding, automatic section naming,
        /// data annotation validation, and startup-time validation.
        /// If no <paramref name="configSectionPath"/> is provided, the section name is inferred
        /// from the type name of <typeparamref name="TOptions"/> (e.g., "MyFeature" for MyFeatureOptions).
        /// <para>
        /// This overload uses reflection-based <see cref="OptionsBuilderDataAnnotationsExtensions.ValidateDataAnnotations{TOptions}"/>
        /// and is <b>not AOT-safe</b>.
        /// </para>
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
        [RequiresDynamicCode("Configuration binding may require dynamic code generation. Use AddOptionsInConfiguration<TOptions, TValidator> for AOT scenarios.")]
        [RequiresUnreferencedCode("Validation via DataAnnotations may require unreferenced types. Use AddOptionsInConfiguration<TOptions, TValidator> for AOT scenarios.")]
        public OptionsBuilder<TOptions> AddOptionsInConfiguration<TOptions>(string? configSectionPath = null)
            where TOptions : class
        {
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

            var existingDescriptor = services.FirstOrDefault(d => d.ServiceType == descriptor.ServiceType);
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
        public IServiceCollection ReplaceOrAddSingleton<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
            where TService : class
            where TImplementation : class, TService
            => services.ReplaceOrAdd(ServiceDescriptor.Singleton<TService, TImplementation>());

        /// <summary>
        /// Replaces an existing singleton service or adds a new one if it doesn't exist.
        /// </summary>
        public IServiceCollection ReplaceOrAddSingleton<TService>(Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(implementationFactory);
            return services.ReplaceOrAdd(ServiceDescriptor.Singleton(implementationFactory));
        }

        /// <summary>
        /// Replaces an existing singleton service or adds a new one if it doesn't exist.
        /// </summary>
        public IServiceCollection ReplaceOrAddSingleton<TService>(TService implementationInstance)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(implementationInstance);
            return services.ReplaceOrAdd(ServiceDescriptor.Singleton(implementationInstance));
        }

        /// <summary>
        /// Replaces an existing scoped service or adds a new one if it doesn't exist.
        /// </summary>
        public IServiceCollection ReplaceOrAddScoped<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
            where TService : class
            where TImplementation : class, TService
            => services.ReplaceOrAdd(ServiceDescriptor.Scoped<TService, TImplementation>());

        /// <summary>
        /// Replaces an existing scoped service or adds a new one if it doesn't exist.
        /// </summary>
        public IServiceCollection ReplaceOrAddScoped<TService>(Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(implementationFactory);
            return services.ReplaceOrAdd(ServiceDescriptor.Scoped(implementationFactory));
        }

        /// <summary>
        /// Replaces an existing transient service or adds a new one if it doesn't exist.
        /// </summary>
        public IServiceCollection ReplaceOrAddTransient<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
            where TService : class
            where TImplementation : class, TService
            => services.ReplaceOrAdd(ServiceDescriptor.Transient<TService, TImplementation>());

        /// <summary>
        /// Replaces an existing transient service or adds a new one if it doesn't exist.
        /// </summary>
        public IServiceCollection ReplaceOrAddTransient<TService>(Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(implementationFactory);
            return services.ReplaceOrAdd(ServiceDescriptor.Transient(implementationFactory));
        }

        /// <summary>
        /// Retrieves the singleton instance of <typeparamref name="T"/> already registered in the service collection,
        /// or creates a new instance using the parameterless constructor, registers it, and returns it.
        /// </summary>
        public T GetOrAddSingleton<T>() where T : class, new()
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(T));
            if (descriptor?.ImplementationInstance is T existing)
            {
                return existing;
            }

            var instance = new T();
            services.AddSingleton(instance);
            return instance;
        }

        /// <summary>
        /// Adds an HTTP client registration where both the client and implementation are of type <typeparamref name="TClient"/>,
        /// and automatically configures the <see cref="HttpClient.BaseAddress"/> from a connection string.
        /// The connection string key is derived from the type name.
        /// </summary>
        public IHttpClientBuilder AddHttpClientWithBaseAddress<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>(Action<IServiceProvider, HttpClient>? configurator = null)
            where TClient : class
            => services.AddHttpClientWithBaseAddress<TClient, TClient>(configurator);

        /// <summary>
        /// Adds an HTTP client registration where both the client and implementation are of type <typeparamref name="TClient"/>,
        /// and configures the <see cref="HttpClient.BaseAddress"/> using the specified <paramref name="serviceName"/>
        /// as the connection string key.
        /// </summary>
        public IHttpClientBuilder AddHttpClientWithBaseAddress<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>(string serviceName, Action<IServiceProvider, HttpClient>? configurator = null)
            where TClient : class
            => services.AddHttpClientWithBaseAddress<TClient, TClient>(serviceName, configurator);

        /// <summary>
        /// Adds an HTTP client registration with separate client and implementation types,
        /// and automatically configures the <see cref="HttpClient.BaseAddress"/> from a connection string.
        /// The connection string key is derived from <typeparamref name="TImplementation"/>.
        /// </summary>
        public IHttpClientBuilder AddHttpClientWithBaseAddress<TClient, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(Action<IServiceProvider, HttpClient>? configurator = null)
            where TClient : class
            where TImplementation : class, TClient
        {
            var serviceName = string.GetSystemNameFromClient<TImplementation>();
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new InvalidOperationException(
                    $"Failed to derive a valid service name from type '{typeof(TImplementation).FullName}'. " +
                    "Ensure the type name follows expected naming conventions.");
            }

            return services.AddHttpClientWithBaseAddress<TClient, TImplementation>(serviceName, configurator);
        }

        /// <summary>
        /// Adds an HTTP client registration with separate client and implementation types,
        /// and configures the <see cref="HttpClient.BaseAddress"/> using the specified <paramref name="serviceName"/>
        /// as the connection string key.
        /// </summary>
        public IHttpClientBuilder AddHttpClientWithBaseAddress<TClient, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(string serviceName, Action<IServiceProvider, HttpClient>? configurator = null)
            where TClient : class
            where TImplementation : class, TClient
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
            return services.AddHttpClient<TClient, TImplementation>((sp, client) =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var baseAddressString = configuration.GetConnectionString(serviceName);

                if (!string.IsNullOrEmpty(baseAddressString))
                {
                    if (!Uri.TryCreate(baseAddressString, UriKind.Absolute, out var baseUri) || !baseUri.IsAbsoluteUri)
                    {
                        throw new InvalidOperationException(
                            $"The connection string '{serviceName}' contains an invalid or relative URI: '{baseAddressString}'. " +
                            "The base address must be an absolute URI (e.g., 'https://api.example.com/').");
                    }

                    client.BaseAddress = baseUri;
                }

                configurator?.Invoke(sp, client);
            });
        }

        public IServiceCollection AddCommandOutboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, ICommandOutboundMiddleware
        {
            services.TryAddScoped<TMiddleware>();
            services.GetOrAddSingleton<CommandOutboundPipelineTypes>().Add<TMiddleware>();
            return services;
        }

        public IServiceCollection AddNotificationOutboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, INotificationOutboundMiddleware
        {
            services.TryAddScoped<TMiddleware>();
            services.GetOrAddSingleton<NotificationOutboundPipelineTypes>().Add<TMiddleware>();
            return services;
        }

        public IServiceCollection AddRequestOutboundMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
            where TMiddleware : class, IRequestOutboundMiddleware
        {
            services.TryAddScoped<TMiddleware>();
            services.GetOrAddSingleton<RequestOutboundPipelineTypes>().Add<TMiddleware>();
            return services;
        }
    }
}
