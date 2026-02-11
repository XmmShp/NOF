using System.Text.Json;
using System.Text.Json.Serialization;

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
            };
    }
}
