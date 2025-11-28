using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;

namespace NOF.Test;

/// <summary>
/// 提供用于HTTP单元测试的辅助方法
/// </summary>
public static class HttpMockHelpers
{
    /// <summary>
    /// 创建一个模拟的HttpMessageHandler，返回预设的响应
    /// </summary>
    /// <param name="statusCode">HTTP状态码</param>
    /// <param name="content">响应内容</param>
    /// <returns>模拟的HttpMessageHandler</returns>
    public static Mock<HttpMessageHandler> CreateMockHttpMessageHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string content = "")
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });

        return mockHandler;
    }

    /// <summary>
    /// 创建一个模拟的HttpMessageHandler，返回预设的API响应
    /// </summary>
    /// <typeparam name="T">响应数据类型</typeparam>
    /// <param name="isSuccess">是否成功</param>
    /// <param name="value">响应值</param>
    /// <param name="errorCode">错误代码</param>
    /// <param name="message">错误消息</param>
    /// <param name="statusCode">HTTP状态码</param>
    /// <returns>模拟的HttpMessageHandler</returns>
    public static Mock<HttpMessageHandler> CreateMockApiResponseHandler<T>(
        bool isSuccess = true,
        T? value = default,
        int errorCode = 0,
        string message = "",
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var apiResponse = new Result<T>
        {
            IsSuccess = isSuccess,
            ErrorCode = errorCode,
            Message = message,
            Value = value!
        };

        var content = JsonSerializer.Serialize(apiResponse, DefaultJsonSerializerOptions.Options);
        return CreateMockHttpMessageHandler(statusCode, content);
    }

    /// <summary>
    /// 创建一个模拟的HttpMessageHandler，返回预设的无数据API响应
    /// </summary>
    /// <param name="isSuccess">是否成功</param>
    /// <param name="errorCode">错误代码</param>
    /// <param name="message">错误消息</param>
    /// <param name="statusCode">HTTP状态码</param>
    /// <returns>模拟的HttpMessageHandler</returns>
    public static Mock<HttpMessageHandler> CreateMockApiResponseHandler(
        bool isSuccess = true,
        int errorCode = 0,
        string message = "",
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var apiResponse = new Result
        {
            IsSuccess = isSuccess,
            ErrorCode = errorCode,
            Message = message
        };

        var content = JsonSerializer.Serialize(apiResponse, DefaultJsonSerializerOptions.Options);
        return CreateMockHttpMessageHandler(statusCode, content);
    }
}
