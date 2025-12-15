using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NOF;

// ReSharper disable once InconsistentNaming
public static partial class __NOF_Contract_Extensions__
{
    /// <param name="services">服务集合</param>
    extension(IServiceCollection services)
    {
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
        public IHttpClientBuilder AddHttpClientWithBaseAddress<TClient>(Action<IServiceProvider, HttpClient>? configurator = null)
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
        public IHttpClientBuilder AddHttpClientWithBaseAddress<TClient>(string serviceName, Action<IServiceProvider, HttpClient>? configurator = null)
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
        public IHttpClientBuilder AddHttpClientWithBaseAddress<TClient, TImplementation>(Action<IServiceProvider, HttpClient>? configurator = null)
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
        public IHttpClientBuilder AddHttpClientWithBaseAddress<TClient, TImplementation>(string serviceName, Action<IServiceProvider, HttpClient>? configurator = null)
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
    }
}
