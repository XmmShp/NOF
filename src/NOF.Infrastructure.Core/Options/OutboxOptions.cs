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
    /// 分布式锁过期时间
    /// 默认值: 30 秒
    /// </summary>
    public TimeSpan LockExpiration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 获取分布式锁的超时时间
    /// 默认值: 100 毫秒
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

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
    /// 重试延迟的基数（秒）
    /// 实际延迟 = 2^(RetryCount) * RetryDelayBase
    /// 默认值: 1 秒
    /// </summary>
    public TimeSpan RetryDelayBase { get; set; } = TimeSpan.FromSeconds(1);
}
