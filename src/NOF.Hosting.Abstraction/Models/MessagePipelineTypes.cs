using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

/// <summary>
/// Ordered middleware types for one concrete pipeline.
/// Types can be added in arbitrary order and are frozen into dependency order on first execution.
/// </summary>
public class MessagePipelineTypes<TMiddlewareContract>
    where TMiddlewareContract : class
{
    private readonly List<DependencyNode> _nodes = [];
    private readonly List<Type> _orderedTypes = [];
    private bool _isFrozen;

    public int Count => _orderedTypes.Count;

    public Type this[int index] => _orderedTypes[index];

    public void Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, TMiddlewareContract
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("Pipeline has been frozen and can no longer be modified.");
        }

        var middlewareType = typeof(TMiddleware);
        if (_nodes.Any(n => ReferenceEquals(n.ExtraInfo, middlewareType)))
        {
            return;
        }

        _nodes.Add(new DependencyNode(middlewareType, DependencyNode.CollectRelatedTypes<TMiddleware>()));
    }

    public void Freeze()
    {
        if (_isFrozen)
        {
            return;
        }

        var graph = new DependencyGraph(_nodes);
        var ordered = graph.GetExecutionOrder().Select(node => (Type)node.ExtraInfo).ToList();

        _orderedTypes.Clear();
        _orderedTypes.AddRange(ordered);
        _isFrozen = true;
    }
}
