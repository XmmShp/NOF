using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Contract;

/// <summary>
/// Extension methods for the NOF.Contract layer.
/// </summary>
public static partial class NOFContractExtensions
{
    private static readonly List<Action<JsonSerializerOptions>> _nofConfigurators = [];

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
        options.Converters.Add(new OptionalConverterFactory());
        options.Converters.Add(new PatchRequestConverterFactory());

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

        // Ensure DefaultJsonTypeInfoResolver is always the last resolver in the chain
        // (last-resort fallback) regardless of configurator registration order.
        var defaultResolver = options.TypeInfoResolverChain
            .OfType<DefaultJsonTypeInfoResolver>()
            .FirstOrDefault();
        if (defaultResolver is not null)
        {
            options.TypeInfoResolverChain.Remove(defaultResolver);
            options.TypeInfoResolverChain.Add(defaultResolver);
        }

        options.TypeInfoResolver = options.TypeInfoResolver
            .WithAddedModifier(OptionalTypeInfoResolverModifier.Modifier);

        return options;
    });

    extension(JsonSerializerOptions)
    {
        /// <summary>
        /// Gets the shared <see cref="JsonSerializerOptions"/> instance used by the NOF framework.
        /// </summary>
        /// <remarks>
        /// By default this includes <see cref="NOFJsonSerializerContext"/> for common primitive types,
        /// <see cref="OptionalConverterFactory"/>, <see cref="PatchRequestConverterFactory"/>,
        /// and the <see cref="OptionalTypeInfoResolverModifier"/>.
        /// Call <see cref="ConfigureNOFJsonSerializerOptions"/> before first access to customize
        /// (e.g. add source-generated contexts for your domain types, value object converters, etc.).
        /// The options are frozen by STJ on first use.
        /// </remarks>
        public static JsonSerializerOptions NOF => _nof.Value;

        /// <summary>
        /// Registers a configurator for the shared <see cref="NOF"/> options instance.
        /// Can be called multiple times; configurators are applied in registration order
        /// when <see cref="NOF"/> is first accessed (like the options pattern).
        /// Must be called before <see cref="NOF"/> is first accessed.
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
        /// Thrown if <see cref="NOF"/> has already been materialized.
        /// </exception>
        public static void ConfigureNOFJsonSerializerOptions(Action<JsonSerializerOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            if (_nof.IsValueCreated)
            {
                throw new InvalidOperationException(
                    $"{nameof(ConfigureNOFJsonSerializerOptions)} must be called before {nameof(NOF)} is first accessed.");
            }
            _nofConfigurators.Add(configure);
        }
    }
}
