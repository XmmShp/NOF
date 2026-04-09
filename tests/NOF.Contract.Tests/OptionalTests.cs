using NOF.Abstraction;
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
        Assert.True(
        opt.HasValue);
        Assert.Equal(42,
        opt.Value);
    }

    [Fact]
    public void None_SetsHasValue_False()
    {
        Optional<int> opt = Optional.None;
        Assert.False(
        opt.HasValue);
    }

    // -----------------------------------------------------------------------
    // Implicit conversion from T (new feature)
    // -----------------------------------------------------------------------

    [Fact]
    public void ImplicitConversion_FromInt_SetsHasValue()
    {
        Optional<int> opt = 99;
        Assert.True(
        opt.HasValue);
        Assert.Equal(99,
        opt.Value);
    }

    [Fact]
    public void ImplicitConversion_FromString_SetsHasValue()
    {
        Optional<string> opt = "hello";
        Assert.True(
        opt.HasValue);
        Assert.Equal("hello",
        opt.Value);
    }

    [Fact]
    public void ImplicitConversion_FromNullString_SetsHasValue_WithNullValue()
    {
        Optional<string> opt = null!;
        Assert.True(
        opt.HasValue);
        Assert.Null(
        opt.Value);
    }

    [Fact]
    public void ImplicitConversion_FromNoneOptional_SetsHasValue_False()
    {
        Optional<int> opt = Optional.None;
        Assert.False(
        opt.HasValue);
    }

    // -----------------------------------------------------------------------
    // ValueOr
    // -----------------------------------------------------------------------

    [Fact]
    public void ValueOr_WhenHasValue_ReturnsValue()
    {
        Optional<int> opt = 5;
        Assert.Equal(5,
        opt.ValueOr(99));
    }

    [Fact]
    public void ValueOr_WhenNone_ReturnsDefault()
    {
        Optional<int> opt = Optional.None;
        Assert.Equal(99,
        opt.ValueOr(99));
    }

    // -----------------------------------------------------------------------
    // Match
    // -----------------------------------------------------------------------

    [Fact]
    public void Match_WhenHasValue_InvokesSomeBranch()
    {
        Optional<int> opt = 7;
        var result = opt.Match(v => v * 2, () => -1);
        Assert.Equal(14,
        result);
    }

    [Fact]
    public void Match_WhenNone_InvokesNoneBranch()
    {
        Optional<int> opt = Optional.None;
        var result = opt.Match(v => v * 2, () => -1);
        Assert.Equal(-1,
        result);
    }
}

public class OptionalJsonTests
{
    private static JsonSerializerOptions Options() => JsonSerializerOptions.NOF;

    // -----------------------------------------------------------------------
    // Serialization 鈥?Optional<T> present
    // -----------------------------------------------------------------------

    [Fact]
    public void Serialize_WhenHasValue_WritesValueAndHasValue()
    {
        var dto = new DtoWithOptional { Name = Optional.Of<string?>("Alice") };
        var json = JsonSerializer.Serialize(dto, Options());
        Assert.Contains("\"name\":", json);
        Assert.Contains("\"hasValue\":true", json);
        Assert.Contains("\"value\":\"Alice\"", json);
    }

    [Fact]
    public void Serialize_WhenNone_WritesValueAndHasValue()
    {
        var dto = new DtoWithOptional { Name = Optional.None };
        var json = JsonSerializer.Serialize(dto, Options());
        Assert.Contains("\"name\":", json);
        Assert.Contains("\"hasValue\":false", json);
    }

    [Fact]
    public void Serialize_NoStackOverflow_OnComplexObject()
    {
        var dto = new DtoWithOptional { Name = Optional.Of<string?>("test"), Age = Optional.Of(30) };
        var act = () => JsonSerializer.Serialize(dto, Options());
        Assert.Null(Record.Exception(act));
    }

    // -----------------------------------------------------------------------
    // Deserialization
    // -----------------------------------------------------------------------

    [Fact]
    public void Deserialize_PresentProperty_SetsHasValue()
    {
        const string json = """{"name":{"hasValue":true,"value":"Bob"},"age":{"hasValue":true,"value":25}}""";
        var opts = Options();
        var dto = JsonSerializer.Deserialize<DtoWithOptional>(json, opts)!;
        Assert.True(dto.Name.HasValue, "converter should set HasValue=true for present property");
        Assert.Equal("Bob", dto.Name.Value);
        Assert.True(dto.Age.HasValue);
        Assert.Equal(25, dto.Age.Value);
    }

    [Fact]
    public void Deserialize_MissingProperty_LeavesDefault()
    {
        const string json = """{"name":{"hasValue":true,"value":"Bob"}}""";
        var opts = Options();
        var dto = JsonSerializer.Deserialize<DtoWithOptional>(json, opts)!;
        Assert.True(dto.Name.HasValue);
        Assert.False(dto.Age.HasValue);
    }

    [Fact]
    public void Deserialize_NullProperty_SetsHasValue_WithNullValue()
    {
        const string json = """{"name":{"hasValue":true,"value":null}}""";
        var opts = Options();
        var dto = JsonSerializer.Deserialize<DtoWithOptional>(json, opts)!;
        Assert.True(dto.Name.HasValue);
        Assert.Null(dto.Name.Value);
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
        Assert.True(restored.Name.HasValue);
        Assert.Equal("Charlie", restored.Name.Value);
        Assert.True(restored.Age.HasValue);
        Assert.Equal(42, restored.Age.Value);
    }

    [Fact]
    public void RoundTrip_AbsentValues_PreservesData()
    {
        var original = new DtoWithOptional { Name = Optional.Of<string?>("Dave"), Age = Optional.None };
        var opts = Options();
        var json = JsonSerializer.Serialize(original, opts);

        Assert.Contains("age", json);
        Assert.Contains("\"hasValue\":false", json);

        var restored = JsonSerializer.Deserialize<DtoWithOptional>(json, opts)!;
        Assert.False(restored.Age.HasValue);
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

