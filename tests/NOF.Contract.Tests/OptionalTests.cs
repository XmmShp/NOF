using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace NOF.Contract.Tests;

public class OptionalTests
{
    // -----------------------------------------------------------------------
    // Construction & HasValue
    // -----------------------------------------------------------------------

    [Fact]
    public void Of_SetsHasValue_True()
    {
        var opt = Optional.Of(42);
        opt.HasValue.Should().BeTrue();
        opt.Value.Should().Be(42);
    }

    [Fact]
    public void None_SetsHasValue_False()
    {
        Optional<int> opt = Optional.None;
        opt.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Value_WhenNone_Throws()
    {
        Optional<int> opt = Optional.None;
        var act = () => opt.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    // -----------------------------------------------------------------------
    // Implicit conversion from T (new feature)
    // -----------------------------------------------------------------------

    [Fact]
    public void ImplicitConversion_FromInt_SetsHasValue()
    {
        Optional<int> opt = 99;
        opt.HasValue.Should().BeTrue();
        opt.Value.Should().Be(99);
    }

    [Fact]
    public void ImplicitConversion_FromString_SetsHasValue()
    {
        Optional<string> opt = "hello";
        opt.HasValue.Should().BeTrue();
        opt.Value.Should().Be("hello");
    }

    [Fact]
    public void ImplicitConversion_FromNullString_SetsHasValue_WithNullValue()
    {
        Optional<string> opt = (string)null!;
        opt.HasValue.Should().BeTrue();
        opt.Value.Should().BeNull();
    }

    [Fact]
    public void ImplicitConversion_FromNoneOptional_SetsHasValue_False()
    {
        Optional<int> opt = Optional.None;
        opt.HasValue.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // ValueOr
    // -----------------------------------------------------------------------

    [Fact]
    public void ValueOr_WhenHasValue_ReturnsValue()
    {
        Optional<int> opt = 5;
        opt.ValueOr(99).Should().Be(5);
    }

    [Fact]
    public void ValueOr_WhenNone_ReturnsDefault()
    {
        Optional<int> opt = Optional.None;
        opt.ValueOr(99).Should().Be(99);
    }

    // -----------------------------------------------------------------------
    // Match
    // -----------------------------------------------------------------------

    [Fact]
    public void Match_WhenHasValue_InvokesSomeBranch()
    {
        Optional<int> opt = 7;
        var result = opt.Match(v => v * 2, () => -1);
        result.Should().Be(14);
    }

    [Fact]
    public void Match_WhenNone_InvokesNoneBranch()
    {
        Optional<int> opt = Optional.None;
        var result = opt.Match(v => v * 2, () => -1);
        result.Should().Be(-1);
    }
}

public class OptionalJsonTests
{
    private static JsonSerializerOptions Options() =>
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        }.AddNOFConverters();

    // -----------------------------------------------------------------------
    // Serialization — Optional<T> present
    // -----------------------------------------------------------------------

    [Fact]
    public void Serialize_WhenHasValue_WritesValue()
    {
        var dto = new DtoWithOptional { Name = Optional.Of("Alice") };
        var json = JsonSerializer.Serialize(dto, Options());
        json.Should().Contain("\"name\":\"Alice\"", because: $"actual json was: {json}");
    }

    [Fact]
    public void Serialize_WhenNone_OmitsProperty()
    {
        var dto = new DtoWithOptional { Name = Optional.None };
        var json = JsonSerializer.Serialize(dto, Options());
        json.Should().NotContain("name");
    }

    [Fact]
    public void Serialize_NoStackOverflow_OnComplexObject()
    {
        var dto = new DtoWithOptional { Name = Optional.Of("test"), Age = Optional.Of(30) };
        var act = () => JsonSerializer.Serialize(dto, Options());
        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    // Deserialization
    // -----------------------------------------------------------------------

    [Fact]
    public void Deserialize_PresentProperty_SetsHasValue()
    {
        const string json = """{"name":"Bob","age":25}""";
        var opts = Options();
        var dto = JsonSerializer.Deserialize<DtoWithOptional>(json, opts)!;
        dto.Name.HasValue.Should().BeTrue(because: $"converter should set HasValue=true for present property");
        dto.Name.Value.Should().Be("Bob");
        dto.Age.HasValue.Should().BeTrue();
        dto.Age.Value.Should().Be(25);
    }

    [Fact]
    public void Deserialize_MissingProperty_LeavesDefault()
    {
        const string json = """{"name":"Bob"}""";
        var opts = Options();
        var dto = JsonSerializer.Deserialize<DtoWithOptional>(json, opts)!;
        dto.Name.HasValue.Should().BeTrue();
        dto.Age.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Deserialize_NullProperty_SetsHasValue_WithNullValue()
    {
        const string json = """{"name":null}""";
        var opts = Options();
        var dto = JsonSerializer.Deserialize<DtoWithOptional>(json, opts)!;
        dto.Name.HasValue.Should().BeTrue();
        dto.Name.Value.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public void RoundTrip_PresentValues_PreservesData()
    {
        var original = new DtoWithOptional { Name = Optional.Of<string?>("Charlie"), Age = Optional.Of(42) };
        var opts = Options();
        var json = JsonSerializer.Serialize(original, opts);
        var restored = JsonSerializer.Deserialize<DtoWithOptional>(json, opts)!;

        restored.Name.HasValue.Should().BeTrue();
        restored.Name.Value.Should().Be("Charlie");
        restored.Age.HasValue.Should().BeTrue();
        restored.Age.Value.Should().Be(42);
    }

    [Fact]
    public void RoundTrip_AbsentValues_OmittedThenMissingOnDeserialize()
    {
        var original = new DtoWithOptional { Name = Optional.Of<string?>("Dave"), Age = Optional.None };
        var opts = Options();
        var json = JsonSerializer.Serialize(original, opts);

        json.Should().NotContain("age");

        var restored = JsonSerializer.Deserialize<DtoWithOptional>(json, opts)!;
        restored.Age.HasValue.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Test DTO
    // -----------------------------------------------------------------------

    private sealed class DtoWithOptional
    {
        public Optional<string?> Name { get; set; }
        public Optional<int> Age { get; set; }
    }
}
