using System.Text.Json;
using System.Text.Json.Serialization;

namespace NOF;

/// <summary>
/// Extension methods for the NOF.Contract layer.
/// </summary>
public static partial class __NOF_Contract_Extensions__
{
    private static JsonSerializerOptions? _nofDefaults;

    extension(JsonSerializerOptions)
    {
        /// <summary>Gets the default <see cref="JsonSerializerOptions"/> used by the NOF framework.</summary>
        public static JsonSerializerOptions NOFDefaults =>
            _nofDefaults ??= new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
    }
}
