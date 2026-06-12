using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Abstraction;

/// <summary>
/// Extension methods for the NOF.Abstraction layer.
/// </summary>
public static partial class NOFAbstractionExtensions
{
    private static readonly List<Action<JsonSerializerOptions>> _nofConfigurators = [];
    private static readonly object _nofSync = new();

    private static readonly Lazy<JsonSerializerOptions> _nof = new(() =>
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            TypeInfoResolver = NOFJsonSerializerContext.Default
        };

        if (JsonSerializer.IsReflectionEnabledByDefault)
        {
#pragma warning disable IL2026, IL3050
            options.TypeInfoResolverChain.Add(new DefaultJsonTypeInfoResolver());
#pragma warning restore IL2026, IL3050
        }

        foreach (var configure in _nofConfigurators)
        {
            configure(options);
        }

        EnsureDefaultResolverIsLast(options);

        return options;
    });

    extension(JsonSerializerOptions options)
    {
        /// <summary>
        /// Gets the shared <see cref="JsonSerializerOptions"/> instance used by the NOF framework.
        /// </summary>
        /// <remarks>
        /// By default this includes <see cref="NOFJsonSerializerContext"/> for common primitive types.
        /// Call <see cref="ConfigureNOFJsonSerializerOptions"/> before first JSON use to customize
        /// (e.g. add source-generated contexts for your domain types, value object converters, etc.).
        /// The options are frozen by STJ on first use.
        /// </remarks>
        public static JsonSerializerOptions NOF => _nof.Value;

        /// <summary>
        /// Gets the source-generated <see cref="JsonTypeInfo{T}"/> for <typeparamref name="T"/> from the current
        /// options instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no metadata for <typeparamref name="T"/> is available in the current resolver chain.
        /// </exception>
        public JsonTypeInfo<T> GetRequiredTypeInfo<T>()
        {
            return options.GetRequiredTypeInfo(typeof(T)) as JsonTypeInfo<T>
                ?? throw CreateMissingMetadataException(typeof(T));
        }

        /// <summary>
        /// Gets the source-generated <see cref="JsonTypeInfo"/> for the specified runtime type from the current
        /// options instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no metadata for <paramref name="runtimeType"/> is available in the current resolver chain.
        /// </exception>
        public JsonTypeInfo GetRequiredTypeInfo(Type runtimeType)
        {
            ArgumentNullException.ThrowIfNull(runtimeType);

            try
            {
                return options.GetTypeInfo(runtimeType);
            }
            catch (NotSupportedException ex)
            {
                throw CreateMissingMetadataException(runtimeType, ex);
            }
        }

        /// <summary>
        /// Registers a configurator for the shared <see cref="NOF"/> options instance.
        /// Can be called multiple times; configurators are applied in registration order
        /// when <see cref="NOF"/> is first used. If the options instance has already been
        /// materialized but is not frozen yet, the configurator is applied immediately.
        /// </summary>
        /// <param name="configure">
        /// An action that receives the <see cref="JsonSerializerOptions"/> instance to configure.
        /// Use this to add source-generated <c>JsonSerializerContext</c> instances to the resolver chain,
        /// register value object converters, or apply any other customizations.
        /// </param>
        /// <example>
        /// <code>
        /// JsonSerializerOptions.ConfigureNOFJsonSerializerOptions(options =>
        /// {
        ///     options.TypeInfoResolverChain.Add(MyAppJsonContext.Default);
        /// });
        /// </code>
        /// </example>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="NOF"/> has already been used by JSON serialization and is now read-only.
        /// </exception>
        public static void ConfigureNOFJsonSerializerOptions(Action<JsonSerializerOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            lock (_nofSync)
            {
                if (_nof.IsValueCreated)
                {
                    try
                    {
                        configure(_nof.Value);
                        EnsureDefaultResolverIsLast(_nof.Value);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new InvalidOperationException(
                            $"{nameof(ConfigureNOFJsonSerializerOptions)} must be called before {nameof(NOF)} is first used for JSON serialization.",
                            ex);
                    }

                    return;
                }

                _nofConfigurators.Add(configure);
            }
        }
    }

    private static void EnsureDefaultResolverIsLast(JsonSerializerOptions options)
    {
        var defaultResolver = options.TypeInfoResolverChain
            .OfType<DefaultJsonTypeInfoResolver>()
            .FirstOrDefault();
        if (defaultResolver is not null)
        {
            options.TypeInfoResolverChain.Remove(defaultResolver);
            options.TypeInfoResolverChain.Add(defaultResolver);
        }
    }

    private static InvalidOperationException CreateMissingMetadataException(Type runtimeType, Exception? innerException = null)
    {
        return new InvalidOperationException(
            $"NOF JSON metadata for '{runtimeType.FullName}' was not found. " +
            $"Register a source-generated JsonSerializerContext via {nameof(ConfigureNOFJsonSerializerOptions)}(...) before the type is first serialized or deserialized.",
            innerException);
    }
}
