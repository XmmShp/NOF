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
        /// <exception cref="KeyNotFoundException">当指定的缓存项不存在时抛出。</exception>
        /// <exception cref="JsonException">当缓存中的数据无法反序列化为指定类型时抛出。</exception>
        public TValue Get<TValue>(CacheKey<TValue> key, JsonSerializerOptions? jsonOptions = null)
            => cache.Get<TValue>(key.Key, jsonOptions);

        /// <summary>
        /// 获取强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <returns>缓存的对象</returns>
        /// <exception cref="KeyNotFoundException">当指定的缓存项不存在时抛出。</exception>
        /// <exception cref="JsonException">当缓存中的数据无法反序列化为指定类型时抛出。</exception>
        public T Get<T>(string key, JsonSerializerOptions? jsonOptions = null)
        {
            var bytes = cache.Get(key) ?? throw new KeyNotFoundException();
            return JsonSerializer.Deserialize<T>(bytes.AsSpan(), jsonOptions ?? DefaultJsonOptions)!;
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
        public bool TryGet<TValue>(CacheKey<TValue> key, [MaybeNullWhen(false)] out TValue value,
            JsonSerializerOptions? jsonOptions = null)
            => cache.TryGet(key.Key, out value, jsonOptions);

        /// <summary>
        /// 尝试获取强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">如果找到，则包含缓存的对象；否则为默认值</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <returns>如果找到缓存项则为true，否则为false</returns>
        public bool TryGet<T>(string key, [MaybeNullWhen(false)] out T value,
            JsonSerializerOptions? jsonOptions = null)
        {
            var bytes = cache.Get(key);
            if (bytes is null || bytes.Length == 0)
            {
                value = default;
                return false;
            }

            try
            {
                value = JsonSerializer.Deserialize<T>(bytes.AsSpan(), jsonOptions ?? DefaultJsonOptions)!;
                return true;
            }
            catch (Exception)
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// 异步获取强类型缓存对象
        /// </summary>
        /// <typeparam name="TValue">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param> 
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <param name="token">取消令牌</param>
        /// <returns>缓存的对象</returns>
        public ValueTask<TValue> GetAsync<TValue>(CacheKey<TValue> key,
            JsonSerializerOptions? jsonOptions = null, CancellationToken token = default)
            => cache.GetAsync<TValue>(key.Key, jsonOptions, token);

        /// <summary>
        /// 异步获取强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param> 
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <param name="token">取消令牌</param>
        /// <returns>缓存的对象</returns>
        public async ValueTask<T> GetAsync<T>(string key,
            JsonSerializerOptions? jsonOptions = null, CancellationToken token = default)
        {
            var bytes = await cache.GetAsync(key, token) ?? throw new KeyNotFoundException();
            return JsonSerializer.Deserialize<T>(bytes.AsSpan(), jsonOptions ?? DefaultJsonOptions)!;
        }

        /// <summary>
        /// 尝试异步获取强类型缓存对象
        /// </summary>
        /// <typeparam name="TValue">缓存对象类型</typeparam>
        /// <param name="key">缓存键（强类型包装）</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>如果找到缓存项则为 (true, value)，否则为 (false, default)</returns>
        public ValueTask<(bool IsSuccess, TValue? Value)> TryGetAsync<TValue>(
            CacheKey<TValue> key,
            JsonSerializerOptions? jsonOptions = null,
            CancellationToken cancellationToken = default)
            => cache.TryGetAsync<TValue>(key.Key, jsonOptions, cancellationToken);

        /// <summary>
        /// 尝试异步获取强类型缓存对象
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="jsonOptions">JSON序列化选项</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>如果找到缓存项则为 (true, value)，否则为 (false, default)</returns>
        public async ValueTask<(bool IsSuccess, T? Value)> TryGetAsync<T>(
            string key,
            JsonSerializerOptions? jsonOptions = null,
            CancellationToken cancellationToken = default)
        {
            var bytes = await cache.GetAsync(key, cancellationToken).ConfigureAwait(false);

            if (bytes is null || bytes.Length == 0)
            {
                return (false, default);
            }

            try
            {
                var value = JsonSerializer.Deserialize<T>(bytes.AsSpan(), jsonOptions ?? DefaultJsonOptions);
                return (true, value);
            }
            catch (Exception)
            {
                return (false, default);
            }
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
        public ValueTask SetAsync<TValue>(CacheKey<TValue> key, TValue value, DistributedCacheEntryOptions? options = null,
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
        public async ValueTask SetAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null,
            JsonSerializerOptions? jsonOptions = null, CancellationToken token = default)
        {
            var json = JsonSerializer.Serialize(value, jsonOptions ?? DefaultJsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            await cache.SetAsync(key, bytes, options ?? DefaultCacheOptions, token);
        }

        /// <summary>
        /// 从缓存中移除指定键对应的缓存项。
        /// </summary>
        /// <typeparam name="TValue">缓存项的类型（仅用于强类型键的类型推断，不影响实际删除操作）</typeparam>
        /// <param name="key">要移除的缓存项的强类型键。</param>
        public void Remove<TValue>(CacheKey<TValue> key)
            => cache.Remove(key.Key);

        /// <summary>
        /// 异步地从缓存中移除指定键对应的缓存项。
        /// </summary>
        /// <typeparam name="TValue">缓存项的类型（仅用于强类型键的类型推断，不影响实际删除操作）</typeparam>
        /// <param name="key">要移除的缓存项的强类型键。</param>
        /// <param name="token">用于取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>表示异步删除操作的任务。</returns>
        public async ValueTask RemoveAsync<TValue>(CacheKey<TValue> key, CancellationToken token = default)
            => await cache.RemoveAsync(key.Key, token);

        /// <summary>
        /// 刷新指定缓存项的滑动过期时间（如果适用），使其不会因过期而被移除。
        /// </summary>
        /// <typeparam name="TValue">缓存项的类型（仅用于强类型键的类型推断，不影响实际刷新操作）</typeparam>
        /// <param name="key">要刷新的缓存项的强类型键。</param>
        public void Refresh<TValue>(CacheKey<TValue> key)
            => cache.Refresh(key.Key);

        /// <summary>
        /// 异步刷新指定缓存项的滑动过期时间（如果适用），使其不会因过期而被移除。
        /// </summary>
        /// <typeparam name="TValue">缓存项的类型（仅用于强类型键的类型推断，不影响实际刷新操作）</typeparam>
        /// <param name="key">要刷新的缓存项的强类型键。</param>
        /// <param name="ct">用于取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>表示异步刷新操作的任务。</returns>
        public async ValueTask RefreshAsync<TValue>(CacheKey<TValue> key, CancellationToken ct = default)
            => await cache.RefreshAsync(key.Key, ct);
    }
}