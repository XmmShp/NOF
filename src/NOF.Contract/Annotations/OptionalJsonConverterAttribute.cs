using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NOF.Contract;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Interface, AllowMultiple = false)]
internal class OptionalJsonConverterAttribute : JsonConverterAttribute
{
    private static readonly ConcurrentDictionary<Type, JsonConverter?> ConverterCache = [];
    public override JsonConverter? CreateConverter(Type typeToConvert)
    {
        return ConverterCache.GetOrAdd(typeToConvert, GetConverter);

        static JsonConverter? GetConverter(Type typeToConvert)
        {
            if (!typeToConvert.IsGenericType || typeToConvert.GetGenericTypeDefinition() != typeof(Optional<>))
            {
                return null;
            }

            var valueType = typeToConvert.GenericTypeArguments[0];
            var converterType = typeof(OptionalConverter<>).MakeGenericType(valueType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }
}

internal class OptionalConverter<T> : JsonConverter<Optional<T>>
{
    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return Optional.Of<T>(default!);
        }

        var value = JsonSerializer.Deserialize<T>(ref reader, options)!;
        return Optional.Of(value);
    }

    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Value, options);
    }
}
