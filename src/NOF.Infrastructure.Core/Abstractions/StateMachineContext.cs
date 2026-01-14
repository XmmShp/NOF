namespace NOF;

/// <summary>
/// 状态机上下文实体
/// 包含状态机上下文、状态和追踪信息
/// </summary>
public sealed class StateMachineContext
{
    /// <summary>
    /// 关联ID，用于标识状态机实例
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// 状态机定义类型
    /// </summary>
    public required Type DefinitionType { get; init; }

    /// <summary>
    /// 状态机上下文实例
    /// </summary>
    public required IStateMachineContext Context { get; init; }

    /// <summary>
    /// 当前状态
    /// </summary>
    public required int State { get; init; }

    /// <summary>
    /// 分布式追踪 TraceId
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// 分布式追踪 SpanId
    /// </summary>
    public string? SpanId { get; init; }

    /// <summary>
    /// 创建新的状态机上下文实例
    /// </summary>
    public static StateMachineContext Create(
        string correlationId,
        Type definitionType,
        IStateMachineContext context,
        int state,
        string? traceId = null,
        string? spanId = null)
    {
        return new StateMachineContext
        {
            CorrelationId = correlationId,
            DefinitionType = definitionType,
            Context = context,
            State = state,
            TraceId = traceId,
            SpanId = spanId
        };
    }
}
