using FluentAssertions;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using Xunit;

namespace NOF.Application.Tests;

public class BuiltInMappingTests
{
    private static ManualMapper CreateMapper(Action<MapperOptions>? configure = null)
    {
        var options = new MapperOptions();
        configure?.Invoke(options);
        return new ManualMapper(Options.Create(options));
    }

    #region T → string (ToString)

    [Fact]
    public void BuiltIn_IntToString()
    {
        var mapper = CreateMapper();
        mapper.Map<int, string>(42).Should().Be("42");
    }

    [Fact]
    public void BuiltIn_DoubleToString()
    {
        var mapper = CreateMapper();
        mapper.Map<double, string>(3.14).Should().Be(3.14.ToString());
    }

    [Fact]
    public void BuiltIn_EnumToString()
    {
        var mapper = CreateMapper();
        mapper.Map<DayOfWeek, string>(DayOfWeek.Monday).Should().Be("Monday");
    }

    [Fact]
    public void BuiltIn_BoolToString()
    {
        var mapper = CreateMapper();
        mapper.Map<bool, string>(true).Should().Be("True");
    }

    [Fact]
    public void BuiltIn_GuidToString()
    {
        var mapper = CreateMapper();
        var guid = Guid.NewGuid();
        mapper.Map<Guid, string>(guid).Should().Be(guid.ToString());
    }

    #endregion

    #region Numeric conversions

    [Fact]
    public void BuiltIn_IntToLong()
    {
        var mapper = CreateMapper();
        mapper.Map<int, long>(42).Should().Be(42L);
    }

    [Fact]
    public void BuiltIn_LongToInt()
    {
        var mapper = CreateMapper();
        mapper.Map<long, int>(42L).Should().Be(42);
    }

    [Fact]
    public void BuiltIn_IntToDouble()
    {
        var mapper = CreateMapper();
        mapper.Map<int, double>(42).Should().Be(42.0);
    }

    [Fact]
    public void BuiltIn_DoubleToInt()
    {
        var mapper = CreateMapper();
        // Convert.ChangeType uses IConvertible.ToInt32 which rounds
        mapper.Map<double, int>(42.0).Should().Be(42);
    }

    [Fact]
    public void BuiltIn_IntToDecimal()
    {
        var mapper = CreateMapper();
        mapper.Map<int, decimal>(42).Should().Be(42m);
    }

    [Fact]
    public void BuiltIn_DecimalToInt()
    {
        var mapper = CreateMapper();
        mapper.Map<decimal, int>(42.5m).Should().Be(42);
    }

    [Fact]
    public void BuiltIn_IntToFloat()
    {
        var mapper = CreateMapper();
        mapper.Map<int, float>(42).Should().Be(42f);
    }

    [Fact]
    public void BuiltIn_IntToShort()
    {
        var mapper = CreateMapper();
        mapper.Map<int, short>(42).Should().Be(42);
    }

    [Fact]
    public void BuiltIn_IntToByte()
    {
        var mapper = CreateMapper();
        mapper.Map<int, byte>(42).Should().Be(42);
    }

    [Fact]
    public void BuiltIn_LongToDecimal()
    {
        var mapper = CreateMapper();
        mapper.Map<long, decimal>(100L).Should().Be(100m);
    }

    #endregion

    #region Enum ↔ numeric

    [Fact]
    public void BuiltIn_EnumToInt()
    {
        var mapper = CreateMapper();
        mapper.Map<DayOfWeek, int>(DayOfWeek.Wednesday).Should().Be(3);
    }

    [Fact]
    public void BuiltIn_IntToEnum()
    {
        var mapper = CreateMapper();
        mapper.Map<int, DayOfWeek>(3).Should().Be(DayOfWeek.Wednesday);
    }

    [Fact]
    public void BuiltIn_EnumToLong()
    {
        var mapper = CreateMapper();
        mapper.Map<DayOfWeek, long>(DayOfWeek.Friday).Should().Be(5L);
    }

    [Fact]
    public void BuiltIn_LongToEnum()
    {
        var mapper = CreateMapper();
        mapper.Map<long, DayOfWeek>(5L).Should().Be(DayOfWeek.Friday);
    }

    #endregion

    #region IValueObject → underlying primitive

    [Fact]
    public void BuiltIn_ValueObjectToUnderlying()
    {
        var mapper = CreateMapper();
        var vo = new FakeIntValueObject(42);
        mapper.Map<FakeIntValueObject, int>(vo).Should().Be(42);
    }

    [Fact]
    public void BuiltIn_ValueObjectToString()
    {
        var mapper = CreateMapper();
        var vo = new FakeStringValueObject("hello");
        mapper.Map<FakeStringValueObject, string>(vo).Should().Be("hello");
    }

    [Fact]
    public void BuiltIn_ValueObjectChainConversion_IntToLong()
    {
        var mapper = CreateMapper();
        var vo = new FakeIntValueObject(42);
        // ValueObject<int> → int → long (chained)
        mapper.Map<FakeIntValueObject, long>(vo).Should().Be(42L);
    }

    [Fact]
    public void BuiltIn_ValueObjectChainConversion_IntToString()
    {
        var mapper = CreateMapper();
        var vo = new FakeIntValueObject(42);
        // ValueObject<int> → int → string (chained via ToString)
        mapper.Map<FakeIntValueObject, string>(vo).Should().Be("42");
    }

    #endregion

    #region Result<T> → T

    [Fact]
    public void BuiltIn_SuccessResultToValue()
    {
        var mapper = CreateMapper();
        var result = Result.Success(42);
        mapper.Map<Result<int>, int>(result).Should().Be(42);
    }

    [Fact]
    public void BuiltIn_FailedResult_ReturnsNone()
    {
        var mapper = CreateMapper();
        Result<int> result = Result.Fail(500, "error");
        var tryResult = mapper.TryMap<Result<int>, int>(result);
        tryResult.HasValue.Should().BeFalse();
    }

    [Fact]
    public void BuiltIn_ResultToString_ExtractsValue()
    {
        var mapper = CreateMapper();
        var result = Result.Success("hello");
        mapper.Map<Result<string>, string>(result).Should().Be("hello");
    }

    #endregion

    #region Optional<T> → T

    [Fact]
    public void BuiltIn_OptionalWithValueToValue()
    {
        var mapper = CreateMapper();
        var opt = Optional.Of(42);
        mapper.Map<Optional<int>, int>(opt).Should().Be(42);
    }

    [Fact]
    public void BuiltIn_OptionalNone_ReturnsNone()
    {
        var mapper = CreateMapper();
        Optional<int> opt = Optional.None;
        var tryResult = mapper.TryMap<Optional<int>, int>(opt);
        tryResult.HasValue.Should().BeFalse();
    }

    [Fact]
    public void BuiltIn_OptionalStringToString()
    {
        var mapper = CreateMapper();
        var opt = Optional.Of("hello");
        mapper.Map<Optional<string>, string>(opt).Should().Be("hello");
    }

    #endregion

    #region User mapping takes priority over built-in

    [Fact]
    public void UserMapping_TakesPriorityOverBuiltIn()
    {
        var mapper = CreateMapper(o =>
            o.Add<int, string>(x => $"custom:{x}"));

        // User mapping should win over built-in ToString
        mapper.Map<int, string>(42).Should().Be("custom:42");
    }

    [Fact]
    public void BuiltIn_NamedMapping_DoesNotApply()
    {
        var mapper = CreateMapper();

        // Built-in mappings only apply for unnamed (default) mappings
        var result = mapper.TryMap<int, string>(42, name: "custom");
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region Test helpers (fake ValueObjects implementing IValueObject<T>)

    private readonly struct FakeIntValueObject : IValueObject<int>
    {
        private readonly int _value;

        public FakeIntValueObject(int value) => _value = value;

        public int GetUnderlyingValue() => _value;
    }

    private readonly struct FakeStringValueObject : IValueObject<string>
    {
        private readonly string _value;

        public FakeStringValueObject(string value) => _value = value;

        public string GetUnderlyingValue() => _value;
    }

    #endregion
}
