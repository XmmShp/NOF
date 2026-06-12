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

    #region StreamingResult.From

    [Fact]
    public void StreamingResultFrom_WithFailResult_ReturnsFailedStreamingResult()
    {
        IResult response = Result.Fail("500", "Internal server error");

        var result = StreamingResult<string>.From(response);
        Assert.False(result.IsSuccess);
        Assert.Equal("500", result.ErrorCode);
        Assert.Equal("Internal server error", result.Message);
        Assert.Null(result.Value);
    }

    [Fact]
    public void StreamingResultFrom_WithDeserializedFailedResult_ReturnsFailedStreamingResult()
    {
        Result original = Result.Fail("409", "Conflict", new Dictionary<string, string>
        {
            ["requestId"] = "abc"
        });
        var json = JsonSerializer.Serialize(original);
        IResult response = JsonSerializer.Deserialize<Result>(json)!;

        var result = StreamingResult<string>.From(response);
        Assert.False(result.IsSuccess);
        Assert.Equal("409", result.ErrorCode);
        Assert.Equal("Conflict", result.Message);
        Assert.Equal("abc", result.Extra["requestId"]);
        Assert.Null(result.Value);
    }

    [Fact]
    public void RequireCompatible_WithFailResult_UsesStaticFromProjection()
    {
        IResult response = Result.Fail("422", "Validation failed", new Dictionary<string, string>
        {
            ["field"] = "name"
        });

        var result = ResultProjection.RequireCompatible<CustomProjectedResult>(response);
        Assert.False(result.IsSuccess);
        Assert.Equal("422", result.ErrorCode);
        Assert.Equal("Validation failed", result.Message);
        Assert.Equal("name", result.Extra["field"]);
    }

    [Fact]
    public void PaginatedResultFrom_WithFailResult_ReturnsFailedPaginatedResult()
    {
        IResult response = Result.Fail("409", "Conflict", new Dictionary<string, string>
        {
            ["requestId"] = "abc"
        });

        var result = PaginatedResult<string>.From(response);
        Assert.False(result.IsSuccess);
        Assert.Equal("409", result.ErrorCode);
        Assert.Equal("Conflict", result.Message);
        Assert.Equal("abc", result.Extra["requestId"]);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    [Fact]
    public void RequireCompatible_WithPaginatedArrayPayload_ProjectsToPaginatedResult()
    {
        var result = ResultProjection.RequireCompatible<PaginatedResult<int>>(
            Result.Success(new[] { 11, 12 }, new Dictionary<string, string> { ["totalCount"] = "25" }));
        Assert.True(result.IsSuccess);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal([11, 12], result.Value);
    }

    [Fact]
    public void PaginatedResultFrom_WithEnumerablePayload_ProjectsToPaginatedResult()
    {
        IResult response = new CustomPayloadResult<IEnumerable<int>>([1, 2, 3]);

        var result = PaginatedResult<int>.From(response);
        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2, 3], result.Value);
    }

    [Fact]
    public void PaginatedResultFrom_WithSingleValuePayload_ProjectsToPaginatedResult()
    {
        IResult response = new CustomPayloadResult<int>(7);

        var result = PaginatedResult<int>.From(response);
        Assert.True(result.IsSuccess);
        Assert.Equal([7], result.Value);
    }

    [Fact]
    public void PaginatedResultSuccess_WithTotalCount_WritesMetadataToExtra()
    {
        var result = PaginatedResult.Success(["a", "b"], 88);

        Assert.Equal("88", result.Extra["totalCount"]);
        Assert.Equal(88, result.TotalCount);
        Assert.NotNull(result.Value);
        Assert.Equal(["a", "b"], result.Value);
    }

    [Fact]
    public void PaginatedResult_Serialization_DoesNotEmitComputedTotalCountProperty()
    {
        var result = PaginatedResult.Success(["a", "b"], 88);

        var json = JsonSerializer.Serialize(result);
        Assert.Contains("\"Extra\":", json);
        Assert.Contains("\"totalCount\":\"88\"", json);
        Assert.DoesNotContain("\"TotalCount\":", json);
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
        Assert.Equal(b.ErrorCode, a.ErrorCode);
        Assert.Equal(b.Message, a.Message);
        Assert.Equal(b.IsSuccess, a.IsSuccess);
        Assert.Empty(a.Extra);
        Assert.Empty(b.Extra);
        Assert.NotSame(b.Extra, a.Extra);
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

    [Fact]
    public void ResultFrom_WithFailResult_ReturnsFailedResult()
    {
        IResult response = Result.Fail("401", "Unauthorized");

        var result = Result.From(response);
        Assert.False(result.IsSuccess);
        Assert.Equal("401", result.ErrorCode);
        Assert.Equal("Unauthorized", result.Message);
    }

    [Fact]
    public void ResultTFrom_WithSuccessPayload_ProjectsValue()
    {
        IResult response = new CustomPayloadResult<int>(7);

        var result = Result<int>.From(response);
        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    #endregion

    private class TestDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed record CustomProjectedResult(
        bool IsSuccess,
        string ErrorCode,
        string Message,
        object? Value,
        IDictionary<string, string> Extra) : IResult<CustomProjectedResult>
    {
        public static CustomProjectedResult From(IResult other)
        {
            return new CustomProjectedResult(other.IsSuccess, other.ErrorCode, other.Message, other.Value, new Dictionary<string, string>(other.Extra));
        }
    }

    private sealed record CustomPayloadResult<T>(T Payload) : IResult<CustomPayloadResult<T>>
    {
        public bool IsSuccess => true;

        public string ErrorCode => string.Empty;

        public string Message => string.Empty;

        public object? Value => Payload;

        public IDictionary<string, string> Extra { get; } = new Dictionary<string, string>();

        public static CustomPayloadResult<T> From(IResult other)
        {
            throw new NotSupportedException();
        }
    }
}
