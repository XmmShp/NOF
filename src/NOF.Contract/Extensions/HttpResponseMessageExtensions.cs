using System.Net.Http.Json;
using System.Text.Json;

namespace NOF;

/// <summary>
/// HttpResponseMessage的扩展方法
/// </summary>
public static partial class __NOF_Contract_Extensions__
{
    // 错误代码常量
    private const int ErrorCodeApiAccess = 400001;
    private const int ErrorCodeResponseParsing = 400002;

    // 错误消息常量
    private const string ErrorMessageApiAccess = "访问后端API时出现错误";
    private const string ErrorMessageResponseParsing = "解析返回结果时发生错误";

    /// <param name="response">HTTP响应</param>
    extension(HttpResponseMessage response)
    {
        /// <summary>
        /// 处理HTTP响应并转换为Result&lt;T&gt;
        /// </summary>
        /// <typeparam name="T">响应数据类型</typeparam>
        /// <returns>API响应</returns>
        public async Task<Result<T>> ToResultAsync<T>()
        {
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
                var apiResponse = await response.Content.ReadFromJsonAsync<Result<T>>(Options);
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
        /// 处理HTTP响应并转换为Result（无数据版本）
        /// </summary>
        /// <returns>API响应</returns>
        public async Task<Result> ToResultAsync()
        {
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
                var apiResponse = await response.Content.ReadFromJsonAsync<Result>(Options);
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
