using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Contract;

/// <summary>
/// Extension methods for the NOF.Contract layer.
/// </summary>
public static partial class NOFContractExtensions
{
    private static readonly Lazy<JsonSerializerOptions> NOFDefaults = new(CreateNOFDefaults);

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "JsonStringEnumConverter is required for enum serialization; specific enum converters are registered by downstream JsonSerializerContexts.")]
    private static JsonSerializerOptions CreateNOFDefaults() =>
        new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }.AddNOFConverters();

    extension(JsonSerializerOptions)
    {
        /// <summary>Gets the default <see cref="JsonSerializerOptions"/> used by the NOF framework.</summary>
        public static JsonSerializerOptions NOFDefaults => NOFDefaults.Value;
    }

    extension(JsonSerializerOptions options)
    {
        /// <summary>
        /// Registers all NOF framework JSON converters and converter factories
        /// into the specified <see cref="JsonSerializerOptions"/>.
        /// </summary>
        /// <returns>The same <see cref="JsonSerializerOptions"/> instance for fluent chaining.</returns>
        [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "DefaultJsonTypeInfoResolver is used as a fallback; AOT apps should add their JsonSerializerContext to the resolver chain.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "DefaultJsonTypeInfoResolver is used as a fallback; AOT apps should add their JsonSerializerContext to the resolver chain.")]
        public JsonSerializerOptions AddNOFConverters()
        {
            options.Converters.Add(new OptionalConverterFactory());
            options.Converters.Add(new PatchRequestConverterFactory());

            var defaultResolver = options.TypeInfoResolverChain
                .OfType<DefaultJsonTypeInfoResolver>()
                .FirstOrDefault();

            if (defaultResolver is null)
            {
                defaultResolver = new DefaultJsonTypeInfoResolver();
                options.TypeInfoResolverChain.Add(defaultResolver);
            }

            defaultResolver.Modifiers.Add(OptionalTypeInfoResolverModifier.Modifier);

            return options;
        }
    }
}
