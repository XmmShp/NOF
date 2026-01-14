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
    /// 重试延迟的基数（秒）
    /// 实际延迟 = (2^(RetryCount - 1)) * RetryDelayBase
    /// 默认值: 1 秒
    /// </summary>
    public TimeSpan RetryDelayBase { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 重试延迟的最大上限（防止退避时间过长）
    /// 默认值: 5 分钟
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
}