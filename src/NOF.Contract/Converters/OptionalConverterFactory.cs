using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Contract;

/// <summary>
/// A converter factory that creates <see cref="OptionalConverter{T}"/> instances
/// for any <see cref="Optional{T}"/> type.
/// </summary>
public class OptionalConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GenericTypeArguments[0];
        var converterType = typeof(OptionalConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
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
        if (!value.HasValue)
        {
            return;
        }

        JsonSerializer.Serialize(writer, value.Value, options);
    }
}

/// <summary>
/// A <see cref="System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver"/> modifier that
/// suppresses serialization of <see cref="Optional{T}"/> properties when
/// <see cref="Optional{T}.HasValue"/> is <c>false</c>, so the property is omitted from the JSON
/// output entirely rather than written as <c>null</c>.
/// </summary>
/// <remarks>
/// Register by adding <see cref="Modifier"/> to
/// <see cref="DefaultJsonTypeInfoResolver.Modifiers"/> (or equivalent resolver).
/// Do NOT add this as a standalone entry in <see cref="JsonSerializerOptions.TypeInfoResolverChain"/>;
/// that would cause infinite recursion because the chain re-enters itself.
/// </remarks>
public static class OptionalTypeInfoResolverModifier
{
    private static readonly Type OptionalOpenType = typeof(Optional<>);

    /// <summary>
    /// A modifier action to be added to <see cref="DefaultJsonTypeInfoResolver.Modifiers"/>.
    /// It patches every <see cref="Optional{T}"/> property so that the property is omitted
    /// when <see cref="Optional{T}.HasValue"/> is <c>false</c>.
    /// </summary>
    public static readonly Action<JsonTypeInfo> Modifier = static typeInfo =>
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (var prop in typeInfo.Properties)
        {
            if (!prop.PropertyType.IsGenericType)
            {
                continue;
            }

            if (prop.PropertyType.GetGenericTypeDefinition() != OptionalOpenType)
            {
                continue;
            }

            var getter = prop.Get;
            if (getter is null)
            {
                continue;
            }

            var hasValueProp = prop.PropertyType.GetProperty(nameof(Optional<int>.HasValue))!;
            prop.ShouldSerialize = (obj, _) =>
            {
                var optional = getter(obj);
                return optional is not null && (bool)hasValueProp.GetValue(optional)!;
            };
        }
    };
}
