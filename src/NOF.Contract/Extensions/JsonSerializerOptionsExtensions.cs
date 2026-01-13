using System.Text.Json;
using System.Text.Json.Serialization;

namespace NOF;

public static partial class __NOF_Contract_Extensions__
{
    private static JsonSerializerOptions? _nofDefaults;

    extension(JsonSerializerOptions)
    {
        public static JsonSerializerOptions NOFDefaults =>
            _nofDefaults ??= new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
    }
}
