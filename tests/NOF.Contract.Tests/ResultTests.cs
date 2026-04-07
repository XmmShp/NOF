using System.Text.Json;
using Xunit;

namespace NOF.Contract.Tests;

public class ResultTests
{
    #region Result.Success()

    [Fact]
    public void Success_ReturnsSuccessResult()
    {
        var result = Result.Success();
        Assert.True(

        result.IsSuccess);
        Assert.Empty(
        result.ErrorCode);
        Assert.Empty(
        result.Message);
    }

    #endregion

    #region Result.Success<T>()

    [Fact]
    public void SuccessT_ReturnsSuccessResultWithValue()
    {
        var result = Result.Success("hello");
        Assert.True(

        result.IsSuccess);
        Assert.Empty(
        result.ErrorCode);
        Assert.Empty(
        result.Message);
        Assert.Equal("hello",
        result.Value);
    }

    [Fact]
    public void SuccessT_WithComplexType_ReturnsValue()
    {
        var dto = new TestDto { Id = 42, Name = "test" };
        var result = Result.Success(dto);
        Assert.True(

        result.IsSuccess);
        Assert.Same(dto, result.Value);
    }

    #endregion

    #region Result.Fail()

    [Fact]
    public void Fail_ReturnsFailResult()
    {
        var fail = Result.Fail("404", "Not found");
        Assert.Equal("404",

        fail.ErrorCode);
        Assert.Equal("Not found",
        fail.Message);
    }

    #endregion

    #region FailResult implicit conversion to Result

    [Fact]
    public void FailResult_ImplicitConversion_ToResult()
    {
        FailResult fail = Result.Fail("500", "Server error");

        Result result = fail;
        Assert.False(

        result.IsSuccess);
        Assert.Equal("500",
        result.ErrorCode);
        Assert.Equal("Server error",
        result.Message);
    }

    [Fact]
    public void FailResult_ImplicitConversion_ToResult_InMethodReturn()
    {
        var result = ReturnResultFromFail();
        Assert.False(

        result.IsSuccess);
        Assert.Equal("403",
        result.ErrorCode);
        Assert.Equal("Forbidden",
        result.Message);
    }

    private static Result ReturnResultFromFail()
    {
        return Result.Fail("403", "Forbidden");
    }

    #endregion

    #region FailResult implicit conversion to Result<T>

    [Fact]
    public void FailResult_ImplicitConversion_ToResultT()
    {
        FailResult fail = Result.Fail("400", "Bad request");

        Result<string> result = fail;
        Assert.False(

        result.IsSuccess);
        Assert.Equal("400",
        result.ErrorCode);
        Assert.Equal("Bad request",
        result.Message);
        Assert.Null(
        result.Value);
    }

    [Fact]
    public void FailResult_ImplicitConversion_ToResultT_InMethodReturn()
    {
        var result = ReturnResultTFromFail();
        Assert.False(

        result.IsSuccess);
        Assert.Equal("401",
        result.ErrorCode);
        Assert.Equal("Unauthorized",
        result.Message);
        Assert.Null(
        result.Value);
    }

    private static Result<TestDto> ReturnResultTFromFail()
    {
        return Result.Fail("401", "Unauthorized");
    }

    #endregion

    #region FailResult runtime cast 閳?the actual bug scenario

    [Fact]
    public void FailResult_DirectRuntimeCast_ToResult_Throws()
    {
        object response = Result.Fail("500", "Internal server error");

        var act = () => (Result)response;
        Assert.Throws<InvalidCastException>(act);
    }

    [Fact]
    public void FailResult_DirectRuntimeCast_ToResultT_Throws()
    {
        object response = Result.Fail("500", "Internal server error");

        var act = () => (Result<string>)response;
        Assert.Throws<InvalidCastException>(act);
    }

    #endregion

    #region Result.From / Result.From<T>

    [Fact]
    public void From_WithFailResult_ReturnsFailedResult()
    {
        IResult response = Result.Fail("500", "Internal server error");

        var result = Result.From(response);
        Assert.False(

        result.IsSuccess);
        Assert.Equal("500",
        result.ErrorCode);
        Assert.Equal("Internal server error",
        result.Message);
    }

    [Fact]
    public void FromT_WithFailResult_ReturnsFailedResultT()
    {
        IResult response = Result.Fail("500", "Internal server error");

        var result = Result.From<string>(response);
        Assert.False(

        result.IsSuccess);
        Assert.Equal("500",
        result.ErrorCode);
        Assert.Equal("Internal server error",
        result.Message);
        Assert.Null(
        result.Value);
    }

    [Fact]
    public void From_WithSuccessResult_ReturnsSuccessResult()
    {
        IResult response = Result.Success();

        var result = Result.From(response);
        Assert.True(

        result.IsSuccess);
    }

    [Fact]
    public void FromT_WithSuccessResultT_ReturnsSuccessResultT()
    {
        IResult response = Result.Success("data");

        var result = Result.From<string>(response);
        Assert.True(

        result.IsSuccess);
        Assert.Equal("data",
        result.Value);
    }

    [Fact]
    public void From_WithNull_Throws()
    {
        var act = () => Result.From(null!);
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void FromT_WithNull_Throws()
    {
        var act = () => Result.From<string>(null!);
        Assert.Throws<ArgumentNullException>(act);
    }

    #endregion

    #region T implicit conversion to Result<T>

    [Fact]
    public void Value_ImplicitConversion_ToResultT()
    {
        Result<int> result = 42;
        Assert.True(

        result.IsSuccess);
        Assert.Equal(42,
        result.Value);
    }

    [Fact]
    public void StringValue_ImplicitConversion_ToResultT()
    {
        Result<string> result = "hello";
        Assert.True(

        result.IsSuccess);
        Assert.Equal("hello",
        result.Value);
    }

    #endregion

    #region JSON serialization round-trip

    [Fact]
    public void Result_Success_JsonRoundTrip()
    {
        var original = Result.Success();
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Result>(json);
        Assert.NotNull(

        deserialized);
        Assert.True(
        deserialized.IsSuccess);
        Assert.Empty(
        deserialized.ErrorCode);
    }

    [Fact]
    public void Result_Fail_JsonRoundTrip()
    {
        Result original = Result.Fail("404", "Not found");
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Result>(json);
        Assert.NotNull(

        deserialized);
        Assert.False(
        deserialized.IsSuccess);
        Assert.Equal("404",
        deserialized.ErrorCode);
        Assert.Equal("Not found",
        deserialized.Message);
    }

    [Fact]
    public void ResultT_Success_JsonRoundTrip()
    {
        var original = Result.Success("hello");
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Result<string>>(json);
        Assert.NotNull(

        deserialized);
        Assert.True(
        deserialized.IsSuccess);
        Assert.Equal("hello",
        deserialized.Value);
    }

    [Fact]
    public void ResultT_Fail_JsonRoundTrip()
    {
        Result<string> original = Result.Fail("500", "Error");
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Result<string>>(json);
        Assert.NotNull(

        deserialized);
        Assert.False(
        deserialized.IsSuccess);
        Assert.Equal("500",
        deserialized.ErrorCode);
        Assert.Equal("Error",
        deserialized.Message);
        Assert.Null(
        deserialized.Value);
    }

    [Fact]
    public void ResultT_ComplexType_JsonRoundTrip()
    {
        var dto = new TestDto { Id = 1, Name = "test" };
        var original = Result.Success(dto);
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Result<TestDto>>(json);
        Assert.NotNull(

        deserialized);
        Assert.True(
        deserialized.IsSuccess);
        Assert.NotNull(
        deserialized.Value);
        Assert.Equal(1,
        deserialized.Value.Id);
        Assert.Equal("test",
        deserialized.Value.Name);
    }

    #endregion

    #region Edge cases

    [Fact]
    public void FailResult_IsIResult()
    {
        var fail = Result.Fail("400", "Bad");
        Assert.True(
        (fail is IResult));
    }

    [Fact]
    public void Result_IsIResult()
    {
        var result = Result.Success();
        Assert.True(
        (result is IResult));
    }

    [Fact]
    public void ResultT_IsIResult()
    {
        var result = Result.Success(42);
        Assert.True(
        (result is IResult));
    }

    [Fact]
    public void FailResult_Record_Equality()
    {
        var a = Result.Fail("404", "Not found");
        var b = Result.Fail("404", "Not found");
        Assert.Equal(b,

        a);
    }

    [Fact]
    public void FailResult_Record_Inequality()
    {
        var a = Result.Fail("404", "Not found");
        var b = Result.Fail("500", "Server error");
        Assert.NotEqual(b,

        a);
    }

    [Fact]
    public void Result_Properties_AreImmutable()
    {
        var result = Result.Success();

        // Properties have private init and cannot be changed after creation.
        Assert.True(result.IsSuccess);
        Assert.Empty(
        result.ErrorCode);
        Assert.Empty(
        result.Message);
    }

    [Fact]
    public void ResultT_Properties_AreImmutable()
    {
        var result = Result.Success("value");
        Assert.True(

        result.IsSuccess);
        Assert.Equal("value",
        result.Value);
        Assert.Empty(
        result.ErrorCode);
        Assert.Empty(
        result.Message);
    }

    #endregion

    private class TestDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}


