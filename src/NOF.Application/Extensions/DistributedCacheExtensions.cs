using Microsoft.Extensions.Caching.Distributed;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace NOF;

/// <summary>
/// 分布式缓存扩展方法，提供强类型支持
/// </summary>
public static class DistributedCacheExtensions
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = DefaultJsonSerializerOptions.Options;
    private static readonly DistributedCacheEntryOptions DefaultCacheOptions = new();

    /// <param name="cache">分布式缓存接口</param>
    extension(IDistributedCache cache)
    {
        /// <summary>
        /// 获取强类型缓存对象
        /// </summary>
        /// <typeparam name="TValue">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <returns>缓存的对象</returns>
        public TValue Get<TValue>(CacheKey<TValue> key, JsonSerializerOptions? jsonOptions = null)
            => cache.Get<TValue>(key.Key, jsonOptions);

        /// <summary>
        /// 获取强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <returns>缓存的对象</returns>
        public T Get<T>(string key, JsonSerializerOptions? jsonOptions = null)
        {
            var bytes = cache.Get(key) ?? throw new KeyNotFoundException();
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json, jsonOptions ?? DefaultJsonOptions)!;
        }

        /// <summary>
        /// 设置强类型缓存对象
        /// </summary>
        /// <typeparam name="TValue">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">要缓存的对象</param>
        /// <param name="options">缓存选项</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        public void Set<TValue>(CacheKey<TValue> key, TValue value,
            DistributedCacheEntryOptions? options = null,
            JsonSerializerOptions? jsonOptions = null)
            => cache.Set(key.Key, value, options, jsonOptions);

        /// <summary>
        /// 设置强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">要缓存的对象</param>
        /// <param name="options">缓存选项</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        public void Set<T>(string key, T value,
            DistributedCacheEntryOptions? options = null, JsonSerializerOptions? jsonOptions = null)
        {
            var json = JsonSerializer.Serialize(value, jsonOptions ?? DefaultJsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            cache.Set(key, bytes, options ?? DefaultCacheOptions);
        }

        /// <summary>
        /// 尝试获取强类型缓存对象
        /// </summary>
        /// <typeparam name="TValue">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">如果找到，则包含缓存的对象；否则为默认值</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <returns>如果找到缓存项则为true，否则为false</returns>
        public bool TryGetValue<TValue>(CacheKey<TValue> key, [MaybeNullWhen(false)] out TValue value,
            JsonSerializerOptions? jsonOptions = null)
            => cache.TryGetValue(key.Key, out value, jsonOptions);

        /// <summary>
        /// 尝试获取强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">如果找到，则包含缓存的对象；否则为默认值</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <returns>如果找到缓存项则为true，否则为false</returns>
        public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value,
            JsonSerializerOptions? jsonOptions = null)
        {
            var bytes = cache.Get(key);
            if (bytes is null || bytes.Length == 0)
            {
                value = default;
                return false;
            }

            var json = Encoding.UTF8.GetString(bytes);
            value = JsonSerializer.Deserialize<T>(json, jsonOptions ?? DefaultJsonOptions)!;
            return true;
        }

        /// <summary>
        /// 异步获取强类型缓存对象
        /// </summary>
        /// <typeparam name="TValue">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param> 
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <param name="token">取消令牌</param>
        /// <returns>缓存的对象</returns>
        public async Task<TValue> GetAsync<TValue>(CacheKey<TValue> key,
            JsonSerializerOptions? jsonOptions = null, CancellationToken token = default)
            => await cache.GetAsync<TValue>(key.Key, jsonOptions, token);

        /// <summary>
        /// 异步获取强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param> 
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <param name="token">取消令牌</param>
        /// <returns>缓存的对象</returns>
        public async Task<T> GetAsync<T>(string key,
            JsonSerializerOptions? jsonOptions = null, CancellationToken token = default)
        {
            var bytes = await cache.GetAsync(key, token) ?? throw new KeyNotFoundException();
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json, jsonOptions ?? DefaultJsonOptions)!;
        }

        /// <summary>
        /// 异步设置强类型缓存对象
        /// </summary>
        /// <typeparam name="TValue">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">要缓存的对象</param>
        /// <param name="options">缓存选项</param> 
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <param name="token">取消令牌</param>
        public Task SetAsync<TValue>(CacheKey<TValue> key, TValue value, DistributedCacheEntryOptions? options = null,
            JsonSerializerOptions? jsonOptions = null, CancellationToken token = default)
            => cache.SetAsync(key.Key, value, options, jsonOptions, token);

        /// <summary>
        /// 异步设置强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">要缓存的对象</param>
        /// <param name="options">缓存选项</param> 
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <param name="token">取消令牌</param>
        public async Task SetAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null,
            JsonSerializerOptions? jsonOptions = null, CancellationToken token = default)
        {
            var json = JsonSerializer.Serialize(value, jsonOptions ?? DefaultJsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            await cache.SetAsync(key, bytes, options ?? DefaultCacheOptions, token);
        }
    }
}