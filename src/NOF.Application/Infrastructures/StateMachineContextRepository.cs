using System.ComponentModel;

namespace NOF;

/// <summary>
/// 状态机上下文实体
/// 包含状态机上下文、状态和追踪信息
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class StateMachineInfo
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
    /// 创建新的状态机上下文实例
    /// </summary>
    public static StateMachineInfo Create(
        string correlationId,
        Type definitionType,
        IStateMachineContext context,
        int state,
        string? traceId = null,
        string? spanId = null)
    {
        return new StateMachineInfo
        {
            CorrelationId = correlationId,
            DefinitionType = definitionType,
            Context = context,
            State = state
        };
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStateMachineContextRepository
{
    ValueTask<StateMachineInfo?> FindAsync(string correlationId, Type definitionType);
    void Add(StateMachineInfo stateMachineInfo);
    void Update(StateMachineInfo stateMachineInfo);
}