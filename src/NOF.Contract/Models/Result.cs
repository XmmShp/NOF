using System.Diagnostics.CodeAnalysis;

namespace NOF;

public interface IResult;

public record Result : IResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 错误代码
    /// </summary>
    public int ErrorCode { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    #region Static Helper
    public static implicit operator Result(FailResult result)
    {
        return new Result
        {
            IsSuccess = false,
            ErrorCode = result.ErrorCode,
            Message = result.Message
        };
    }

    public static FailResult Fail(int errorCode, string message)
    {
        return new FailResult
        {
            ErrorCode = errorCode,
            Message = message
        };
    }

    public static Result Success()
    {
        return new Result
        {
            IsSuccess = true,
            ErrorCode = 0
        };
    }

    public static Result<T> Success<T>(T value)
    {
        return new Result<T>
        {
            IsSuccess = true,
            ErrorCode = 0,
            Message = string.Empty,
            Value = value
        };
    }
    #endregion
}

public record FailResult : IResult
{
    public required int ErrorCode { get; init; }
    public required string Message { get; init; }
}

public record Result<T> : IResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess
    {
        [MemberNotNullWhen(true, nameof(Value))]
        get;
        init;
    }

    /// <summary>
    /// 错误代码
    /// </summary>
    public int ErrorCode { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 返回的数据
    /// </summary>
    public T? Value { get; init; }

    public static implicit operator Result<T>(FailResult result)
    {
        return new Result<T>
        {
            IsSuccess = false,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            Value = default
        };
    }

    public static implicit operator Result<T>(T value)
        => Result.Success(value);
}
