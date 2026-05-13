using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Xunit;

namespace NOF.Abstraction.Tests;

public sealed class JsonSerializerOptionsExtensionsTests
{
    [Fact]
    public void GetRequiredTypeInfo_Generic_ShouldThrowHelpfulMessage_WhenMetadataMissing()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = new EmptyJsonTypeInfoResolver()
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.GetRequiredTypeInfo<MissingJsonType>());
        Assert.Contains(typeof(MissingJsonType).FullName!, ex.Message);
        Assert.Contains("ConfigureNOFJsonSerializerOptions", ex.Message);
        Assert.IsType<NotSupportedException>(ex.InnerException);
    }

    [Fact]
    public void GetRequiredTypeInfo_RuntimeType_ShouldThrowHelpfulMessage_WhenMetadataMissing()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = new EmptyJsonTypeInfoResolver()
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.GetRequiredTypeInfo(typeof(MissingJsonType)));
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
