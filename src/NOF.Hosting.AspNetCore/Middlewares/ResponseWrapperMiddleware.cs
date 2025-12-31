using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace NOF;

/// <summary>
/// API响应处理中间件，统一处理响应格式和异常
/// </summary>
internal class ResponseWrapperMiddleware : IMiddleware
{
    private readonly ILogger<ResponseWrapperMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonSerializerOptions = DefaultJsonSerializerOptions.Options;

    /// <summary>
    /// 创建API响应处理中间件的新实例
    /// </summary>
    /// <param name="logger">用于记录异常的日志记录器</param>
    public ResponseWrapperMiddleware(ILogger<ResponseWrapperMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // 检查是否为OpenAPI文档请求、OPTIONS请求、WebSocket请求或SignalR请求
        if (IsOpenApiRequest(context.Request)
            || IsOptionsRequest(context.Request)
            || IsSignalRRequest(context.Request))
        {
            await next(context);
            return;
        }

        // 替换响应流，以便我们可以读取和修改响应
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await next(context);

            switch (context.Response.StatusCode)
            {
                // 检查是否为重定向或协议提升响应，如果是则不包装
                case >= 100 and < 200:
                case >= 300 and < 400:
                    context.Response.Body = originalBodyStream;
                    responseBody.Seek(0, SeekOrigin.Begin);
                    await responseBody.CopyToAsync(originalBodyStream);
                    break;
                // 处理成功的响应
                case >= 200 and < 300:
                    await HandleSuccessfulResponseAsync(context, responseBody, originalBodyStream);
                    break;
                default:
                    // 处理非成功的HTTP状态码
                    await HandleErrorResponseAsync(context, originalBodyStream, context.Response.StatusCode, "请求处理失败");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发生未处理的异常: {Message}", ex.Message);
            await HandleExceptionAsync(context, originalBodyStream);
        }
    }

    private static async Task HandleSuccessfulResponseAsync(
        HttpContext context,
        MemoryStream responseBody,
        Stream originalBodyStream)
    {
        context.Response.Body = originalBodyStream;

        if (IsApiContentType(context.Response))
        {
            responseBody.Seek(0, SeekOrigin.Begin);
            var responseContent = await new StreamReader(responseBody).ReadToEndAsync();

            if (!string.IsNullOrEmpty(responseContent))
            {
                // 检查响应是否已经是Result或Result<T>类型
                if (IsResultType(responseContent))
                {
                    // 已经是Result类型，直接返回原始内容
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    await context.Response.WriteAsync(responseContent);
                }
                else
                {
                    // 尝试解析响应内容并包装为Result<T>
                    var responseObject = JsonSerializer.Deserialize<object>(responseContent);
                    var successResponse = new Result<object?>
                    {
                        IsSuccess = true,
                        Value = responseObject
                    };

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    await WriteJsonResponseAsync(context, successResponse);
                }
            }
            else
            {
                // 空响应处理
                var successResponse = new Result
                {
                    IsSuccess = true
                };
                await WriteJsonResponseAsync(context, successResponse);
            }
        }
        else
        {
            // 非JSON响应，直接传递原始内容
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    /// <summary>
    /// 检查JSON字符串是否已经是<see cref="Result"/>或<see cref="Result{T}"/>类型
    /// </summary>
    private static bool IsResultType(string jsonContent)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            // 检查是否包含Result类型的特征属性
            return root.ValueKind == JsonValueKind.Object &&
                   root.TryGetProperty("isSuccess", out _);
        }
        catch
        {
            return false;
        }
    }

    private static async Task HandleErrorResponseAsync(
        HttpContext context,
        Stream originalBodyStream,
        int statusCode,
        string message)
    {
        context.Response.Body = originalBodyStream;
        context.Response.StatusCode = (int)HttpStatusCode.OK; // 始终返回200

        var errorResponse = new Result
        {
            IsSuccess = false,
            ErrorCode = statusCode,
            Message = message
        };
        await WriteJsonResponseAsync(context, errorResponse);
    }

    private static async Task HandleExceptionAsync(
        HttpContext context,
        Stream originalBodyStream)
    {
        context.Response.Body = originalBodyStream;
        context.Response.StatusCode = (int)HttpStatusCode.OK; // 始终返回200

        var errorResponse = new Result
        {
            IsSuccess = false,
            ErrorCode = 500,
            Message = "服务器内部错误"
        };
        await WriteJsonResponseAsync(context, errorResponse);
    }

    private static async Task WriteJsonResponseAsync(HttpContext context, object response)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var json = JsonSerializer.Serialize(response, JsonSerializerOptions);
        await context.Response.WriteAsync(json);
    }

    internal static bool IsApiContentType(HttpResponse response) => response.ContentType?.Contains("application/json", StringComparison.CurrentCultureIgnoreCase) ?? true;

    /// <summary>
    /// 检查请求是否为OpenAPI文档请求（Swagger相关）
    /// </summary>
    internal static bool IsOpenApiRequest(HttpRequest request)
    {
        var path = request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        return path.Contains("/swagger") ||
               path.Contains("/openapi") ||
               path.EndsWith(".json") && path.Contains("/swagger/") ||
               path.Contains("/api-docs");
    }

    /// <summary>
    /// 检查请求是否为OPTIONS请求
    /// </summary>
    internal static bool IsOptionsRequest(HttpRequest request)
    {
        return HttpMethods.IsOptions(request.Method);
    }

    /// <summary>
    /// 检查请求是否为SignalR请求
    /// </summary>
    internal static bool IsSignalRRequest(HttpRequest request)
    {
        var path = request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // SignalR包含这些路径模式
        return path.Contains("/hubs/") ||
               path.Contains("/hub/") ||
               path.Contains("/_blazor/") ||
               path.EndsWith("/negotiate") ||
               path.Contains("/signalr/");
    }
}
