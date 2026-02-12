using System.Text.Json;
using System.Text.Json.Serialization;

namespace NOF.Contract;

/// <summary>
/// A converter factory that handles all types derived from <see cref="PatchRequest"/>.
/// Reads/writes only the <see cref="PatchRequest.ExtensionData"/> dictionary,
/// completely bypassing subclass property discovery by STJ.
/// </summary>
public class PatchRequestConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsAssignableTo(typeof(PatchRequest));

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(PatchRequestConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

internal class PatchRequestConverter<T> : JsonConverter<T> where T : PatchRequest, new()
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token.");
        }

        var data = new Dictionary<string, JsonElement>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            var propertyName = reader.GetString()!;
            reader.Read();
            data[propertyName] = JsonElement.ParseValue(ref reader);
        }

        return new T { ExtensionData = data.Count > 0 ? data : null };
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value.ExtensionData is not null)
        {
            foreach (var kvp in value.ExtensionData)
            {
                writer.WritePropertyName(kvp.Key);
                kvp.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }
}
