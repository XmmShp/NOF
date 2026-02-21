using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Contract;

/// <summary>
/// Extension methods for the NOF.Contract layer.
/// </summary>
public static partial class NOFContractExtensions
{
    private static JsonSerializerOptions? NOFDefaults;

    extension(JsonSerializerOptions)
    {
        /// <summary>Gets the default <see cref="JsonSerializerOptions"/> used by the NOF framework.</summary>
        public static JsonSerializerOptions NOFDefaults =>
            NOFDefaults ??= new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            }.AddNOFConverters();
    }

    extension(JsonSerializerOptions options)
    {
        /// <summary>
        /// Registers all NOF framework JSON converters and converter factories
        /// into the specified <see cref="JsonSerializerOptions"/>.
        /// </summary>
        /// <returns>The same <see cref="JsonSerializerOptions"/> instance for fluent chaining.</returns>
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
