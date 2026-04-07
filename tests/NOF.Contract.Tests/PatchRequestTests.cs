using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Xunit;

namespace NOF.Contract.Tests;

public class PatchRequestTests
{
    #region Test DTOs

    private record TestPatchRequest : PatchRequest
    {
        public Optional<string> Name
        {
            get => Get<string>();
            set => Set(value);
        }

        public Optional<int?> Age
        {
            get => Get<int?>();
            set => Set(value);
        }

        public Optional<string?> NickName
        {
            get => Get<string?>();
            set => Set(value);
        }

        public Optional<AddressDto> Address
        {
            get => Get<AddressDto>();
            set => Set(value);
        }
    }

    private record AddressDto
    {
        public string City { get; set; } = string.Empty;
        public string? Street { get; set; }
    }

    #endregion

    #region Deserialization 鈥?present vs absent vs null

    [Fact]
    public void Deserialize_MissingProperty_ReturnsNone()
    {
        var json = """{}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;
        Assert.False(

        patch.Name.HasValue);
        Assert.False(
        patch.Age.HasValue);
    }

    [Fact]
    public void Deserialize_PropertyWithValue_ReturnsSome()
    {
        var json = """{"name": "Alice"}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;
        Assert.True(

        patch.Name.HasValue);
        Assert.Equal("Alice",
        patch.Name.Value);
    }

    [Fact]
    public void Deserialize_PropertyWithNull_ReturnsSomeWithDefault()
    {
        var json = """{"nickName": null}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;
        Assert.True(

        patch.NickName.HasValue);
        Assert.Null(
        patch.NickName.Value);
    }

    [Fact]
    public void Deserialize_IntProperty_ReturnsSome()
    {
        var json = """{"age": 30}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;
        Assert.True(

        patch.Age.HasValue);
        Assert.Equal(30,
        patch.Age.Value);
    }

    [Fact]
    public void Deserialize_NullableIntWithNull_ReturnsSomeWithDefault()
    {
        var json = """{"age": null}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;
        Assert.True(

        patch.Age.HasValue);
        Assert.Null(
        patch.Age.Value);
    }

    [Fact]
    public void Deserialize_MultipleProperties_MixedPresence()
    {
        var json = """{"name": "Bob", "age": null}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;
        Assert.True(

        patch.Name.HasValue);
        Assert.Equal("Bob",
        patch.Name.Value);
        Assert.True(

        patch.Age.HasValue);
        Assert.Null(
        patch.Age.Value);
        Assert.False(

        patch.NickName.HasValue);
    }

    [Fact]
    public void Deserialize_ComplexType_ReturnsSome()
    {
        var json = """{"address": {"city": "Shanghai", "street": "Nanjing Rd"}}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;
        Assert.True(

        patch.Address.HasValue);
        Assert.Equal("Shanghai",
        patch.Address.Value.City);
        Assert.Equal("Nanjing Rd",
        patch.Address.Value.Street);
    }

    [Fact]
    public void Deserialize_ComplexTypeWithNull_ReturnsSomeWithDefault()
    {
        var json = """{"address": null}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;
        Assert.True(

        patch.Address.HasValue);
        Assert.Null(
        patch.Address.Value);
    }

    #endregion

    #region Set 鈥?write back

    [Fact]
    public void Set_Value_WritesToExtensionData()
    {
        var patch = new TestPatchRequest();

        patch.Name = Optional.Of("Charlie");
        Assert.NotNull(

        patch.ExtensionData);
        Assert.True(
        patch.Name.HasValue);
        Assert.Equal("Charlie",
        patch.Name.Value);
    }

    [Fact]
    public void Set_None_RemovesFromExtensionData()
    {
        var json = """{"name": "Alice"}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;
        Assert.True(

        patch.Name.HasValue);

        patch.Name = Optional.None;
        Assert.False(

        patch.Name.HasValue);
    }

    [Fact]
    public void Set_ComplexType_RoundTrips()
    {
        var patch = new TestPatchRequest();
        var address = new AddressDto { City = "Beijing", Street = "Chang'an Ave" };

        patch.Address = Optional.Of(address);
        Assert.True(

        patch.Address.HasValue);
        Assert.Equal("Beijing",
        patch.Address.Value.City);
        Assert.Equal("Chang'an Ave",
        patch.Address.Value.Street);
    }

    [Fact]
    public void Set_ReadModifyWrite_Works()
    {
        var json = """{"address": {"city": "Shanghai", "street": "Nanjing Rd"}}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;

        var addr = patch.Address.Value;
        addr = addr with { City = "Beijing" };
        patch.Address = Optional.Of(addr);
        Assert.Equal("Beijing",

        patch.Address.Value.City);
        Assert.Equal("Nanjing Rd",
        patch.Address.Value.Street);
    }

    #endregion

    #region Serialization round-trip

    [Fact]
    public void Serialize_OnlyIncludesSetProperties()
    {
        var patch = new TestPatchRequest { Name = Optional.Of("Alice") };

        var json = JsonSerializer.Serialize(patch, JsonSerializerOptions.NOF);
        var doc = JsonDocument.Parse(json);
        Assert.True(

        doc.RootElement.TryGetProperty("name", out _));
        Assert.False(
        doc.RootElement.TryGetProperty("age", out _));
        Assert.False(
        doc.RootElement.TryGetProperty("nickName", out _));
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip()
    {
        var original = new TestPatchRequest
        {
            Name = Optional.Of("Alice"),
            Age = Optional.Of<int?>(25)
        };

        var json = JsonSerializer.Serialize(original, JsonSerializerOptions.NOF);
        var deserialized = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;
        Assert.True(

        deserialized.Name.HasValue);
        Assert.Equal("Alice",
        deserialized.Name.Value);
        Assert.True(

        deserialized.Age.HasValue);
        Assert.Equal(25,
        deserialized.Age.Value);
        Assert.False(

        deserialized.NickName.HasValue);
    }

    #endregion

    #region Case insensitivity

    [Fact]
    public void Get_WithCaseInsensitiveOptions_MatchesDifferentCase()
    {
        // Simulate extension data with PascalCase key (e.g., from a non-standard source)
        var json = """{"Name": "Alice"}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;

        var name = patch.Get<string>("Name", JsonSerializerOptions.NOF);
        Assert.True(
        name.HasValue);
        Assert.Equal("Alice",
        name.Value);
    }

    [Fact]
    public void Get_WithCaseSensitiveOptions_DoesNotMatchDifferentCase()
    {
        // Default NOF options use camelCase naming policy and is case-insensitive
        // Create strict options for this test
        var strictOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = null,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        var patch = new TestPatchRequest
        {
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["Name"] = JsonSerializer.SerializeToElement("Alice")
            }
        };

        // Looking for "name" (camelCase of "Name") won't find "Name" with case-sensitive + no naming policy
        // propertyName = "name", key = "name" (no policy 鈫?as-is), dict has "Name" 鈫?miss
        var result = patch.Get<string>("name", strictOptions);
        Assert.False(
        result.HasValue);
    }

    #endregion

    #region Edge cases

    [Fact]
    public void Get_OnEmptyPatch_ReturnsNone()
    {
        var patch = new TestPatchRequest();
        Assert.False(

        patch.Name.HasValue);
        Assert.False(
        patch.Age.HasValue);
        Assert.False(
        patch.Address.HasValue);
    }

    [Fact]
    public void Deserialize_EmptyJson_AllPropertiesNone()
    {
        var json = """{}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;
        Assert.False(

        patch.Name.HasValue);
        Assert.False(
        patch.Age.HasValue);
        Assert.False(
        patch.NickName.HasValue);
        Assert.False(
        patch.Address.HasValue);
    }

    [Fact]
    public void Deserialize_UnknownProperties_CapturedInExtensionData()
    {
        var json = """{"name": "Alice", "unknownField": 42}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOF)!;
        Assert.True(

        patch.Name.HasValue);
        Assert.True(patch.ExtensionData.ContainsKey("unknownField"));
    }

    [Fact]
    public void Set_MultipleTimes_OverwritesPrevious()
    {
        var patch = new TestPatchRequest();

        patch.Name = Optional.Of("First");
        patch.Name = Optional.Of("Second");
        Assert.Equal("Second",

        patch.Name.Value);
    }

    [Fact]
    public void Set_ThenSetNone_RemovesKey()
    {
        var patch = new TestPatchRequest();

        patch.Name = Optional.Of("Alice");
        Assert.True(
        patch.Name.HasValue);

        patch.Name = Optional.None;
        Assert.False(
        patch.Name.HasValue);
    }

    #endregion
}


