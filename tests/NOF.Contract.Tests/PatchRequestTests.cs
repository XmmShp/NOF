using FluentAssertions;
using System.Text.Json;
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

    #region Deserialization — present vs absent vs null

    [Fact]
    public void Deserialize_MissingProperty_ReturnsNone()
    {
        var json = """{}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOFDefaults)!;

        patch.Name.HasValue.Should().BeFalse();
        patch.Age.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Deserialize_PropertyWithValue_ReturnsSome()
    {
        var json = """{"name": "Alice"}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOFDefaults)!;

        patch.Name.HasValue.Should().BeTrue();
        patch.Name.Value.Should().Be("Alice");
    }

    [Fact]
    public void Deserialize_PropertyWithNull_ReturnsSomeWithDefault()
    {
        var json = """{"nickName": null}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOFDefaults)!;

        patch.NickName.HasValue.Should().BeTrue();
        patch.NickName.Value.Should().BeNull();
    }

    [Fact]
    public void Deserialize_IntProperty_ReturnsSome()
    {
        var json = """{"age": 30}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOFDefaults)!;

        patch.Age.HasValue.Should().BeTrue();
        patch.Age.Value.Should().Be(30);
    }

    [Fact]
    public void Deserialize_NullableIntWithNull_ReturnsSomeWithDefault()
    {
        var json = """{"age": null}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOFDefaults)!;

        patch.Age.HasValue.Should().BeTrue();
        patch.Age.Value.Should().BeNull();
    }

    [Fact]
    public void Deserialize_MultipleProperties_MixedPresence()
    {
        var json = """{"name": "Bob", "age": null}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOFDefaults)!;

        patch.Name.HasValue.Should().BeTrue();
        patch.Name.Value.Should().Be("Bob");

        patch.Age.HasValue.Should().BeTrue();
        patch.Age.Value.Should().BeNull();

        patch.NickName.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Deserialize_ComplexType_ReturnsSome()
    {
        var json = """{"address": {"city": "Shanghai", "street": "Nanjing Rd"}}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOFDefaults)!;

        patch.Address.HasValue.Should().BeTrue();
        patch.Address.Value.City.Should().Be("Shanghai");
        patch.Address.Value.Street.Should().Be("Nanjing Rd");
    }

    [Fact]
    public void Deserialize_ComplexTypeWithNull_ReturnsSomeWithDefault()
    {
        var json = """{"address": null}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOFDefaults)!;

        patch.Address.HasValue.Should().BeTrue();
        patch.Address.Value.Should().BeNull();
    }

    #endregion

    #region Set — write back

    [Fact]
    public void Set_Value_WritesToExtensionData()
    {
        var patch = new TestPatchRequest();

        patch.Name = Optional.Of("Charlie");

        patch.ExtensionData.Should().NotBeNull();
        patch.Name.HasValue.Should().BeTrue();
        patch.Name.Value.Should().Be("Charlie");
    }

    [Fact]
    public void Set_None_RemovesFromExtensionData()
    {
        var json = """{"name": "Alice"}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOFDefaults)!;

        patch.Name.HasValue.Should().BeTrue();

        patch.Name = Optional.None;

        patch.Name.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Set_ComplexType_RoundTrips()
    {
        var patch = new TestPatchRequest();
        var address = new AddressDto { City = "Beijing", Street = "Chang'an Ave" };

        patch.Address = Optional.Of(address);

        patch.Address.HasValue.Should().BeTrue();
        patch.Address.Value.City.Should().Be("Beijing");
        patch.Address.Value.Street.Should().Be("Chang'an Ave");
    }

    [Fact]
    public void Set_ReadModifyWrite_Works()
    {
        var json = """{"address": {"city": "Shanghai", "street": "Nanjing Rd"}}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOFDefaults)!;

        var addr = patch.Address.Value;
        addr = addr with { City = "Beijing" };
        patch.Address = Optional.Of(addr);

        patch.Address.Value.City.Should().Be("Beijing");
        patch.Address.Value.Street.Should().Be("Nanjing Rd");
    }

    #endregion

    #region Serialization round-trip

    [Fact]
    public void Serialize_OnlyIncludesSetProperties()
    {
        var patch = new TestPatchRequest { Name = Optional.Of("Alice") };

        var json = JsonSerializer.Serialize(patch, JsonSerializerOptions.NOFDefaults);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("name", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("age", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("nickName", out _).Should().BeFalse();
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip()
    {
        var original = new TestPatchRequest
        {
            Name = Optional.Of("Alice"),
            Age = Optional.Of<int?>(25)
        };

        var json = JsonSerializer.Serialize(original, JsonSerializerOptions.NOFDefaults);
        var deserialized = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOFDefaults)!;

        deserialized.Name.HasValue.Should().BeTrue();
        deserialized.Name.Value.Should().Be("Alice");

        deserialized.Age.HasValue.Should().BeTrue();
        deserialized.Age.Value.Should().Be(25);

        deserialized.NickName.HasValue.Should().BeFalse();
    }

    #endregion

    #region Case insensitivity

    [Fact]
    public void Get_WithCaseInsensitiveOptions_MatchesDifferentCase()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }.AddNOFConverters();

        // Simulate extension data with PascalCase key (e.g., from a non-standard source)
        var json = """{"Name": "Alice"}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, options)!;

        var name = patch.Get<string>("Name", options);
        name.HasValue.Should().BeTrue();
        name.Value.Should().Be("Alice");
    }

    [Fact]
    public void Get_WithCaseSensitiveOptions_DoesNotMatchDifferentCase()
    {
        // Default NOFDefaults uses camelCase naming policy and is case-insensitive
        // Create strict options for this test
        var strictOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = null
        };

        var patch = new TestPatchRequest
        {
            ExtensionData = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["Name"] = JsonSerializer.SerializeToElement("Alice")
            }
        };

        // Looking for "name" (camelCase of "Name") won't find "Name" with case-sensitive + no naming policy
        // propertyName = "name", key = "name" (no policy → as-is), dict has "Name" → miss
        var result = patch.Get<string>("name", strictOptions);
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region Edge cases

    [Fact]
    public void Get_OnEmptyPatch_ReturnsNone()
    {
        var patch = new TestPatchRequest();

        patch.Name.HasValue.Should().BeFalse();
        patch.Age.HasValue.Should().BeFalse();
        patch.Address.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Deserialize_EmptyJson_AllPropertiesNone()
    {
        var json = """{}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOFDefaults)!;

        patch.Name.HasValue.Should().BeFalse();
        patch.Age.HasValue.Should().BeFalse();
        patch.NickName.HasValue.Should().BeFalse();
        patch.Address.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Deserialize_UnknownProperties_CapturedInExtensionData()
    {
        var json = """{"name": "Alice", "unknownField": 42}""";
        var patch = JsonSerializer.Deserialize<TestPatchRequest>(json, JsonSerializerOptions.NOFDefaults)!;

        patch.Name.HasValue.Should().BeTrue();
        patch.ExtensionData.Should().ContainKey("unknownField");
    }

    [Fact]
    public void Set_MultipleTimes_OverwritesPrevious()
    {
        var patch = new TestPatchRequest();

        patch.Name = Optional.Of("First");
        patch.Name = Optional.Of("Second");

        patch.Name.Value.Should().Be("Second");
    }

    [Fact]
    public void Set_ThenSetNone_RemovesKey()
    {
        var patch = new TestPatchRequest();

        patch.Name = Optional.Of("Alice");
        patch.Name.HasValue.Should().BeTrue();

        patch.Name = Optional.None;
        patch.Name.HasValue.Should().BeFalse();
    }

    #endregion
}
