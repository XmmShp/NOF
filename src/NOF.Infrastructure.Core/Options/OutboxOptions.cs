namespace NOF;

/// <summary>
/// Outbox 模式的配置选项
/// </summary>
public sealed class OutboxOptions
{
    /// <summary>
    /// 轮询间隔
    /// 默认值: 5 秒
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 每次轮询获取的最大消息数量
    /// 默认值: 100
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// 消息发送的最大重试次数
    /// 默认值: 5
    /// </summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>
    /// 抢占锁超时时间（防止实例崩溃导致永久死锁）
    /// 默认值: 5 分钟
    /// </summary>
    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(5);
}