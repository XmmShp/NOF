namespace NOF.Sample.Application.Entities;

/// <summary>
/// 配置节点子节点信息（读模型实体，用于查询优化）
/// </summary>
public class ConfigNodeChildren
{
    /// <summary>
    /// 节点ID
    /// </summary>
    public ConfigNodeId NodeId { get; init; }

    /// <summary>
    /// 子节点ID列表
    /// </summary>
    public List<long> ChildrenIds { get; init; } = [];

    internal ConfigNodeChildren() { }

    public static ConfigNodeChildren Create(ConfigNodeId nodeId)
    {
        return new ConfigNodeChildren
        {
            NodeId = nodeId
        };
    }

    public void AddChild(ConfigNodeId childId)
    {
        if (!ChildrenIds.Contains(childId.Value))
        {
            ChildrenIds.Add(childId.Value);
        }
    }

    public void RemoveChild(ConfigNodeId childId)
    {
        ChildrenIds.Remove(childId.Value);
    }

    public bool HasChildren() => ChildrenIds.Count > 0;
}
