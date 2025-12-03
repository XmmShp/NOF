using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

[assembly: InternalsVisibleTo("NOF.Contract.Tests")]

namespace NOF;

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
        /// <param name="url">请求URL</param>
        /// <returns>API响应</returns>
        public async Task<Result> SendGetRequestAsync(string url)
        {
            var response = await httpClient.GetAsync(url);
            return await response.ToResultAsync();
        }

        /// <summary>
        /// 发送GET请求并处理响应
        /// </summary>
        /// <typeparam name="T">响应数据类型</typeparam>
        /// <param name="url">请求URL</param>
        /// <returns>API响应</returns>
        public async Task<Result<T>> SendGetRequestAsync<T>(string url)
        {
            var response = await httpClient.GetAsync(url);
            return await response.ToResultAsync<T>();
        }

        /// <summary>
        /// 发送POST请求并处理响应
        /// </summary>
        /// <typeparam name="TResponse">响应数据类型</typeparam>
        /// <param name="url">请求URL</param>
        /// <param name="data">请求数据</param>
        /// <returns>API响应</returns>
        public async Task<Result<TResponse>> SendPostRequestAsync<TResponse>(string url, IRequest<TResponse> data)
        {
            var content = GetJsonContent(data);
            var response = await httpClient.PostAsync(url, content);
            return await response.ToResultAsync<TResponse>();
        }

        /// <summary>
        /// 发送POST请求并处理无返回数据的响应
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <param name="data">请求数据</param>
        /// <returns>API响应</returns>
        public async Task<Result> SendPostRequestAsync(string url, IRequest data)
        {
            var content = GetJsonContent(data);
            var response = await httpClient.PostAsync(url, content);
            return await response.ToResultAsync();
        }
    }
    internal static JsonContent GetJsonContent(object data)
    {
        return JsonContent.Create(data, data.GetType(), options: Options);
    }
}