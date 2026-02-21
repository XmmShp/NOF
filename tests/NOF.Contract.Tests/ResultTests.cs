using FluentAssertions;
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

        result.IsSuccess.Should().BeTrue();
        result.ErrorCode.Should().Be(0);
        result.Message.Should().BeEmpty();
    }

    #endregion

    #region Result.Success<T>()

    [Fact]
    public void SuccessT_ReturnsSuccessResultWithValue()
    {
        var result = Result.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.ErrorCode.Should().Be(0);
        result.Message.Should().BeEmpty();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void SuccessT_WithComplexType_ReturnsValue()
    {
        var dto = new TestDto { Id = 42, Name = "test" };
        var result = Result.Success(dto);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    #endregion

    #region Result.Fail()

    [Fact]
    public void Fail_ReturnsFailResult()
    {
        var fail = Result.Fail(404, "Not found");

        fail.ErrorCode.Should().Be(404);
        fail.Message.Should().Be("Not found");
    }

    #endregion

    #region FailResult implicit conversion to Result

    [Fact]
    public void FailResult_ImplicitConversion_ToResult()
    {
        FailResult fail = Result.Fail(500, "Server error");

        Result result = fail;

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(500);
        result.Message.Should().Be("Server error");
    }

    [Fact]
    public void FailResult_ImplicitConversion_ToResult_InMethodReturn()
    {
        var result = ReturnResultFromFail();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(403);
        result.Message.Should().Be("Forbidden");
    }

    private static Result ReturnResultFromFail()
    {
        return Result.Fail(403, "Forbidden");
    }

    #endregion

    #region FailResult implicit conversion to Result<T>

    [Fact]
    public void FailResult_ImplicitConversion_ToResultT()
    {
        FailResult fail = Result.Fail(400, "Bad request");

        Result<string> result = fail;

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(400);
        result.Message.Should().Be("Bad request");
        result.Value.Should().BeNull();
    }

    [Fact]
    public void FailResult_ImplicitConversion_ToResultT_InMethodReturn()
    {
        var result = ReturnResultTFromFail();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(401);
        result.Message.Should().Be("Unauthorized");
        result.Value.Should().BeNull();
    }

    private static Result<TestDto> ReturnResultTFromFail()
    {
        return Result.Fail(401, "Unauthorized");
    }

    #endregion

    #region FailResult runtime cast — the actual bug scenario

    [Fact]
    public void FailResult_DirectRuntimeCast_ToResult_Throws()
    {
        object response = Result.Fail(500, "Internal server error");

        var act = () => (Result)response;
        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void FailResult_DirectRuntimeCast_ToResultT_Throws()
    {
        object response = Result.Fail(500, "Internal server error");

        var act = () => (Result<string>)response;
        act.Should().Throw<InvalidCastException>();
    }

    #endregion

    #region Result.From / Result.From<T>

    [Fact]
    public void From_WithFailResult_ReturnsFailedResult()
    {
        IResult response = Result.Fail(500, "Internal server error");

        var result = Result.From(response);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(500);
        result.Message.Should().Be("Internal server error");
    }

    [Fact]
    public void FromT_WithFailResult_ReturnsFailedResultT()
    {
        IResult response = Result.Fail(500, "Internal server error");

        var result = Result.From<string>(response);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(500);
        result.Message.Should().Be("Internal server error");
        result.Value.Should().BeNull();
    }

    [Fact]
    public void From_WithSuccessResult_ReturnsSuccessResult()
    {
        IResult response = Result.Success();

        var result = Result.From(response);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void FromT_WithSuccessResultT_ReturnsSuccessResultT()
    {
        IResult response = Result.Success("data");

        var result = Result.From<string>(response);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("data");
    }

    [Fact]
    public void From_WithNull_Throws()
    {
        var act = () => Result.From(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromT_WithNull_Throws()
    {
        var act = () => Result.From<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region T implicit conversion to Result<T>

    [Fact]
    public void Value_ImplicitConversion_ToResultT()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void StringValue_ImplicitConversion_ToResultT()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    #endregion

    #region JSON serialization round-trip

    [Fact]
    public void Result_Success_JsonRoundTrip()
    {
        var original = Result.Success();
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Result>(json);

        deserialized.Should().NotBeNull();
        deserialized!.IsSuccess.Should().BeTrue();
        deserialized.ErrorCode.Should().Be(0);
    }

    [Fact]
    public void Result_Fail_JsonRoundTrip()
    {
        Result original = Result.Fail(404, "Not found");
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Result>(json);

        deserialized.Should().NotBeNull();
        deserialized!.IsSuccess.Should().BeFalse();
        deserialized.ErrorCode.Should().Be(404);
        deserialized.Message.Should().Be("Not found");
    }

    [Fact]
    public void ResultT_Success_JsonRoundTrip()
    {
        var original = Result.Success("hello");
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Result<string>>(json);

        deserialized.Should().NotBeNull();
        deserialized!.IsSuccess.Should().BeTrue();
        deserialized.Value.Should().Be("hello");
    }

    [Fact]
    public void ResultT_Fail_JsonRoundTrip()
    {
        Result<string> original = Result.Fail(500, "Error");
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Result<string>>(json);

        deserialized.Should().NotBeNull();
        deserialized!.IsSuccess.Should().BeFalse();
        deserialized.ErrorCode.Should().Be(500);
        deserialized.Message.Should().Be("Error");
        deserialized.Value.Should().BeNull();
    }

    [Fact]
    public void ResultT_ComplexType_JsonRoundTrip()
    {
        var dto = new TestDto { Id = 1, Name = "test" };
        var original = Result.Success(dto);
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Result<TestDto>>(json);

        deserialized.Should().NotBeNull();
        deserialized!.IsSuccess.Should().BeTrue();
        deserialized.Value.Should().NotBeNull();
        deserialized.Value!.Id.Should().Be(1);
        deserialized.Value.Name.Should().Be("test");
    }

    #endregion

    #region Edge cases

    [Fact]
    public void FailResult_IsIResult()
    {
        var fail = Result.Fail(400, "Bad");
        (fail is IResult).Should().BeTrue();
    }

    [Fact]
    public void Result_IsIResult()
    {
        var result = Result.Success();
        (result is IResult).Should().BeTrue();
    }

    [Fact]
    public void ResultT_IsIResult()
    {
        var result = Result.Success(42);
        (result is IResult).Should().BeTrue();
    }

    [Fact]
    public void FailResult_Record_Equality()
    {
        var a = Result.Fail(404, "Not found");
        var b = Result.Fail(404, "Not found");

        a.Should().Be(b);
    }

    [Fact]
    public void FailResult_Record_Inequality()
    {
        var a = Result.Fail(404, "Not found");
        var b = Result.Fail(500, "Server error");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Result_Properties_AreImmutable()
    {
        var result = Result.Success();

        // Properties have private init — cannot be changed after creation
        result.IsSuccess.Should().BeTrue();
        result.ErrorCode.Should().Be(0);
        result.Message.Should().BeEmpty();
    }

    [Fact]
    public void ResultT_Properties_AreImmutable()
    {
        var result = Result.Success("value");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("value");
        result.ErrorCode.Should().Be(0);
        result.Message.Should().BeEmpty();
    }

    #endregion

    private class TestDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
