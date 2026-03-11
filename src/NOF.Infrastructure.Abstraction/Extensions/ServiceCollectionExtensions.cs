using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Infrastructure.Core;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure.Abstraction;

// ReSharper disable once InconsistentNaming
public static class NOFInfrastructureCoreExtensions
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
        /// <typeparam name="TService">The type of the service to register.</typeparam>
        /// <typeparam name="TImplementation">The implementation type of the service.</typeparam>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection ReplaceOrAddSingleton<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
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
        public IServiceCollection ReplaceOrAddScoped<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
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
        public IServiceCollection ReplaceOrAddTransient<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
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
        /// Retrieves the singleton instance of <typeparamref name="T"/> already registered in the service collection,
        /// or creates a new instance using the parameterless constructor, registers it, and returns it.
        /// </summary>
        /// <typeparam name="T">The singleton service type. Must have a parameterless constructor.</typeparam>
        /// <returns>The existing or newly created singleton instance.</returns>
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
        /// <typeparam name="TClient">The type of the HTTP client. Must be a class.</typeparam>
        /// <param name="configurator">
        /// Optional callback to further configure the <see cref="HttpClient"/> instance (e.g., default headers).
        /// </param>
        /// <returns>An <see cref="IHttpClientBuilder"/> for additional configuration.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the derived service name is null, empty, or whitespace.
        /// </exception>
        public IHttpClientBuilder AddHttpClientWithBaseAddress<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>(Action<IServiceProvider, HttpClient>? configurator = null)
            where TClient : class
            => services.AddHttpClientWithBaseAddress<TClient, TClient>(configurator);

        /// <summary>
        /// Adds an HTTP client registration where both the client and implementation are of type <typeparamref name="TClient"/>,
        /// and configures the <see cref="HttpClient.BaseAddress"/> using the specified <paramref name="serviceName"/>
        /// as the connection string key.
        /// </summary>
        /// <typeparam name="TClient">The type of the HTTP client. Must be a class.</typeparam>
        /// <param name="serviceName">
        /// The name of the connection string in configuration that contains the base address.
        /// Must not be null, empty, or whitespace.
        /// </param>
        /// <param name="configurator">
        /// Optional callback to further configure the <see cref="HttpClient"/> instance.
        /// </param>
        /// <returns>An <see cref="IHttpClientBuilder"/> for additional configuration.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="serviceName"/> is null, empty, or consists only of whitespace.
        /// </exception>
        public IHttpClientBuilder AddHttpClientWithBaseAddress<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>(string serviceName, Action<IServiceProvider, HttpClient>? configurator = null)
            where TClient : class
            => services.AddHttpClientWithBaseAddress<TClient, TClient>(serviceName, configurator);

        /// <summary>
        /// Adds an HTTP client registration with separate client and implementation types,
        /// and automatically configures the <see cref="HttpClient.BaseAddress"/> from a connection string.
        /// The connection string key is derived from <typeparamref name="TImplementation"/>.
        /// </summary>
        /// <typeparam name="TClient">The interface or abstract client type.</typeparam>
        /// <typeparam name="TImplementation">The concrete implementation type. Must be a class implementing <typeparamref name="TClient"/>.</typeparam>
        /// <param name="configurator">
        /// Optional callback to further configure the <see cref="HttpClient"/> instance.
        /// </param>
        /// <returns>An <see cref="IHttpClientBuilder"/> for additional configuration.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the derived service name from <typeparamref name="TImplementation"/> is null, empty, or whitespace.
        /// </exception>
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
        /// <typeparam name="TClient">The interface or abstract client type.</typeparam>
        /// <typeparam name="TImplementation">The concrete implementation type. Must be a class implementing <typeparamref name="TClient"/>.</typeparam>
        /// <param name="serviceName">
        /// The name of the connection string in configuration that contains the base URI.
        /// Must not be null, empty, or whitespace.
        /// </param>
        /// <param name="configurator">
        /// Optional callback to further configure the <see cref="HttpClient"/> instance.
        /// </param>
        /// <returns>An <see cref="IHttpClientBuilder"/> for additional configuration.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="serviceName"/> is null, empty, or consists only of whitespace.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the connection string exists but is not a valid absolute URI.
        /// </exception>
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
                    // Validate URI format early to fail fast during DI resolution
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

        /// <summary>
        /// Adds one or more <see cref="HandlerInfo"/> entries to the <see cref="HandlerInfos"/> singleton.
        /// Each entry is dispatched to the appropriate typed set via pattern matching.
        /// Keyed service registrations are deferred to <c>HandlerKeyedServiceRegistrationStep</c>.
        /// </summary>
        public IServiceCollection AddHandlerInfo(params HandlerInfo[] infos)
        {
            var set = services.GetOrAddSingleton<HandlerInfos>();
            foreach (var info in infos)
            {
                set.Add(info);
            }

            return services;
        }
    }
}
