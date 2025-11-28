using System.Net.Http.Json;
using System.Text.Json;

namespace NOF;

/// <summary>
/// HttpResponseMessage的扩展方法
/// </summary>
public static class HttpResponseMessageExtensions
{
    // 错误代码常量
    private const int ErrorCodeApiAccess = 400001;
    private const int ErrorCodeResponseParsing = 400002;

    // 错误消息常量
    private const string ErrorMessageApiAccess = "访问后端API时出现错误";
    private const string ErrorMessageResponseParsing = "解析返回结果时发生错误";

    private static readonly JsonSerializerOptions Options = DefaultJsonSerializerOptions.Options;

    /// <param name="response">HTTP响应</param>
    extension(HttpResponseMessage response)
    {
        /// <summary>
        /// 处理HTTP响应并转换为ApiResponse&lt;T&gt;
        /// </summary>
        /// <typeparam name="T">响应数据类型</typeparam>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <returns>API响应</returns>
        public async Task<Result<T>> ToApiResponseAsync<T>(JsonSerializerOptions? jsonOptions = null)
        {
            jsonOptions ??= Options;
            if (!response.IsSuccessStatusCode)
            {
                return new Result<T>
                {
                    IsSuccess = false,
                    ErrorCode = ErrorCodeApiAccess,
                    Message = $"{ErrorMessageApiAccess}: {(int)response.StatusCode} {response.ReasonPhrase}",
                    Value = default!
                };
            }

            try
            {
                var apiResponse = await response.Content.ReadFromJsonAsync<Result<T>>(jsonOptions);
                if (apiResponse is null)
                {
                    return new Result<T>
                    {
                        IsSuccess = false,
                        ErrorCode = ErrorCodeResponseParsing,
                        Message = ErrorMessageResponseParsing,
                        Value = default!
                    };
                }
                return apiResponse;
            }
            catch (JsonException)
            {
                return new Result<T>
                {
                    IsSuccess = false,
                    ErrorCode = ErrorCodeResponseParsing,
                    Message = ErrorMessageResponseParsing,
                    Value = default!
                };
            }
        }

        /// <summary>
        /// 处理HTTP响应并转换为ApiResponse（无数据版本）
        /// </summary>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <returns>API响应</returns>
        public async Task<Result> ToApiResponseAsync(JsonSerializerOptions? jsonOptions = null)
        {
            jsonOptions ??= Options;
            if (!response.IsSuccessStatusCode)
            {
                return new Result
                {
                    IsSuccess = false,
                    ErrorCode = ErrorCodeApiAccess,
                    Message = $"{ErrorMessageApiAccess}: {(int)response.StatusCode} {response.ReasonPhrase}"
                };
            }

            try
            {
                var apiResponse = await response.Content.ReadFromJsonAsync<Result>(jsonOptions);
                if (apiResponse is null)
                {
                    return new Result
                    {
                        IsSuccess = false,
                        ErrorCode = ErrorCodeResponseParsing,
                        Message = ErrorMessageResponseParsing
                    };
                }

                return apiResponse;
            }
            catch (JsonException)
            {
                return new Result
                {
                    IsSuccess = false,
                    ErrorCode = ErrorCodeResponseParsing,
                    Message = ErrorMessageResponseParsing
                };
            }
        }
    }
}
