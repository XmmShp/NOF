using NOF;
using System.Net.Http.Json;
using System.Text.Json;

namespace KoalaApp.Common;

/// <summary>
/// HttpClient的扩展方法，用于处理API请求和响应
/// </summary>
public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions Options = DefaultJsonSerializerOptions.Options;

    /// <param name="httpClient">HTTP客户端</param>
    extension(HttpClient httpClient)
    {
        /// <summary>
        /// 发送GET请求并处理响应
        /// </summary>
        /// <typeparam name="T">响应数据类型</typeparam>
        /// <param name="url">请求URL</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <returns>API响应</returns>
        public async Task<Result<T>> SendGetRequestAsync<T>(string url,
            JsonSerializerOptions? jsonOptions = null)
        {
            jsonOptions ??= Options;
            var response = await httpClient.GetAsync(url);
            return await response.ToApiResponseAsync<T>(jsonOptions);
        }

        /// <summary>
        /// 发送POST请求并处理响应
        /// </summary>
        /// <typeparam name="TRequest">请求数据类型</typeparam>
        /// <typeparam name="TResponse">响应数据类型</typeparam>
        /// <param name="url">请求URL</param>
        /// <param name="data">请求数据</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <returns>API响应</returns>
        public async Task<Result<TResponse>> SendPostRequestAsync<TRequest, TResponse>(string url,
            TRequest data,
            JsonSerializerOptions? jsonOptions = null)
        {
            jsonOptions ??= Options;
            var response = await httpClient.PostAsJsonAsync(url, data, jsonOptions);
            return await response.ToApiResponseAsync<TResponse>(jsonOptions);
        }

        /// <summary>
        /// 发送POST请求并处理无返回数据的响应
        /// </summary>
        /// <typeparam name="TRequest">请求数据类型</typeparam>
        /// <param name="url">请求URL</param>
        /// <param name="data">请求数据</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <returns>API响应</returns>
        public async Task<Result> SendPostRequestAsync<TRequest>(string url,
            TRequest data,
            JsonSerializerOptions? jsonOptions = null)
        {
            jsonOptions ??= Options;
            var response = await httpClient.PostAsJsonAsync(url, data, jsonOptions);
            return await response.ToApiResponseAsync(jsonOptions);
        }
    }
}