using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public sealed class JsonObjectSerializerTests
{
    [Fact]
    public void Serialize_ShouldThrowHelpfulMessage_WhenMetadataMissing()
    {
        var serializer = new JsonObjectSerializer(new JsonSerializerOptions
        {
            TypeInfoResolver = new EmptyJsonTypeInfoResolver()
        });

        var ex = Assert.Throws<InvalidOperationException>(() => serializer.Serialize(new MissingJsonType { Name = "demo" }));
        Assert.Contains(typeof(MissingJsonType).FullName!, ex.Message);
        Assert.Contains("ConfigureNOFJsonSerializerOptions", ex.Message);
        Assert.IsType<NotSupportedException>(ex.InnerException);
    }

    [Fact]
    public void Deserialize_ShouldThrowHelpfulMessage_WhenMetadataMissing()
    {
        var serializer = new JsonObjectSerializer(new JsonSerializerOptions
        {
            TypeInfoResolver = new EmptyJsonTypeInfoResolver()
        });

        var ex = Assert.Throws<InvalidOperationException>(() => serializer.Deserialize("""{"name":"demo"}"""u8.ToArray(), typeof(MissingJsonType)));
        Assert.Contains(typeof(MissingJsonType).FullName!, ex.Message);
        Assert.Contains("serialized or deserialized", ex.Message);
        Assert.IsType<NotSupportedException>(ex.InnerException);
    }

    private sealed class MissingJsonType
    {
        public required string Name { get; init; }
    }

    private sealed class EmptyJsonTypeInfoResolver : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            return null;
        }
    }
}
