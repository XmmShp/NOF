using KoalaApp.Common;
using Microsoft.Extensions.Caching.Distributed;
using System.Text;
using System.Text.Json;

namespace NOF;

/// <summary>
/// 分布式缓存扩展方法，提供强类型支持
/// </summary>
public static class DistributedCacheExtensions
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = DefaultJsonSerializerOptions.Options;

    /// <param name="cache">分布式缓存接口</param>
    extension(IDistributedCache cache)
    {
        /// <summary>
        /// 获取强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <returns>缓存的对象，如果不存在则返回默认值</returns>
        public T? Get<T>(string key, JsonSerializerOptions? jsonOptions = null)
        {
            var bytes = cache.Get(key);
            if (bytes is null || bytes.Length == 0)
            {
                return default;
            }

            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json, jsonOptions ?? DefaultJsonOptions);
        }

        /// <summary>
        /// 设置强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">要缓存的对象</param>
        /// <param name="options">缓存选项</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        public void Set<T>(string key, T? value, DistributedCacheEntryOptions? options = null, JsonSerializerOptions? jsonOptions = null)
        {
            if (value is null)
            {
                cache.Remove(key);
                return;
            }

            var json = JsonSerializer.Serialize(value, jsonOptions ?? DefaultJsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            cache.Set(key, bytes, options ?? new DistributedCacheEntryOptions());
        }

        /// <summary>
        /// 尝试获取强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">如果找到，则包含缓存的对象；否则为默认值</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <returns>如果找到缓存项则为true，否则为false</returns>
        public bool TryGetValue<T>(string key, out T? value, JsonSerializerOptions? jsonOptions = null)
        {
            var bytes = cache.Get(key);
            if (bytes is null || bytes.Length == 0)
            {
                value = default;
                return false;
            }

            var json = Encoding.UTF8.GetString(bytes);
            value = JsonSerializer.Deserialize<T>(json, jsonOptions ?? DefaultJsonOptions);
            return true;
        }

        /// <summary>
        /// 异步获取强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param> 
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <param name="token">取消令牌</param>
        /// <returns>缓存的对象，如果不存在则返回默认值</returns>
        public async Task<T?> GetAsync<T>(string key, JsonSerializerOptions? jsonOptions = null, CancellationToken token = default)
        {
            var bytes = await cache.GetAsync(key, token);
            if (bytes is null || bytes.Length == 0)
            {
                return default;
            }

            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json, jsonOptions ?? DefaultJsonOptions);
        }

        /// <summary>
        /// 异步设置强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">要缓存的对象</param>
        /// <param name="options">缓存选项</param> 
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <param name="token">取消令牌</param>
        public async Task SetAsync<T>(string key, T? value, DistributedCacheEntryOptions? options = null, JsonSerializerOptions? jsonOptions = null, CancellationToken token = default)
        {
            if (value is null)
            {
                await cache.RemoveAsync(key, token);
                return;
            }

            var json = JsonSerializer.Serialize(value, jsonOptions ?? DefaultJsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            await cache.SetAsync(key, bytes, options ?? new DistributedCacheEntryOptions(), token);
        }

        /// <summary>
        /// 异步尝试获取强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <param name="token">取消令牌</param>
        /// <returns>包含是否找到和值的元组</returns>
        public async Task<(bool exists, T? value)> TryGetValueAsync<T>(string key, JsonSerializerOptions? jsonOptions = null, CancellationToken token = default)
        {
            var bytes = await cache.GetAsync(key, token);
            if (bytes is null || bytes.Length == 0)
            {
                return (false, default);
            }

            var json = Encoding.UTF8.GetString(bytes);
            var value = JsonSerializer.Deserialize<T>(json, jsonOptions ?? DefaultJsonOptions);
            return (true, value);
        }
    }
}