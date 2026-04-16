namespace NOF.Hosting;

/// <summary>
/// Represents a directed acyclic graph (DAG) that supports topological sorting
/// based on explicit dependencies declared via the <see cref="IAfter{TDependency}"/> and <see cref="IBefore{TDependency}"/> interfaces.
/// This ensures nodes are executed in a valid order where dependencies run before their dependents.
/// </summary>
public sealed class DependencyGraph<T>
{
    private readonly HashSet<DependencyNode> _nodes;
    private IReadOnlyList<DependencyNode>? _orderedNodes;

    private readonly Dictionary<Type, HashSet<DependencyNode>> _typeNodeMap;
    private readonly Type _focusType = typeof(T);

    public DependencyGraph(IEnumerable<DependencyNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        _typeNodeMap = [];
        var nodesSet = new HashSet<DependencyNode>();

        foreach (var node in nodes)
        {
            if (!nodesSet.Add(node))
            {
                continue;
            }
            IndexNode(node);
        }

        _nodes = nodesSet;
    }

    private void IndexNode(DependencyNode node)
    {
        foreach (var relatedType in node.AllAssignableTypes)
        {
            if (!_focusType.IsAssignableFrom(relatedType))
            {
                continue;
            }

            if (!_typeNodeMap.TryGetValue(relatedType, out var nodes))
            {
                nodes = [];
                _typeNodeMap[relatedType] = nodes;
            }
            nodes.Add(node);
        }
    }

    public IReadOnlyList<DependencyNode> GetExecutionOrder()
    {
        if (_orderedNodes is not null)
        {
            return _orderedNodes;
        }

        var graph = _nodes.ToDictionary(n => n, _ => new HashSet<DependencyNode>());
        var inDegree = _nodes.ToDictionary(n => n, _ => 0);

        foreach (var node in _nodes)
        {
            foreach (var iface in node.AllAssignableTypes)
            {
                if (!iface.IsGenericType)
                {
                    continue;
                }

                var def = iface.GetGenericTypeDefinition();
                var contract = iface.GenericTypeArguments[0];

                if (def == typeof(IAfter<>))
                {
                    if (!_typeNodeMap.TryGetValue(contract, out var providers))
                    {
                        continue;
                    }
                    foreach (var provider in providers)
                    {
                        graph[provider].Add(node);
                        inDegree[node]++;
                    }
                }
                else if (def == typeof(IBefore<>))
                {
                    if (!_typeNodeMap.TryGetValue(contract, out var followers))
                    {
                        continue;
                    }
                    foreach (var follower in followers)
                    {
                        graph[node].Add(follower);
                        inDegree[follower]++;
                    }
                }
            }
        }

        var queue = new Queue<DependencyNode>(_nodes.Where(n => inDegree[n] == 0));
        var result = new List<DependencyNode>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var dependent in graph[current])
            {
                if (--inDegree[dependent] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        if (result.Count != _nodes.Count)
        {
            throw new InvalidOperationException(
                "Circular dependency detected among configurators. " +
                "Please ensure that dependency chains declared via IAfter<> / IBefore<> do not form cycles.");
        }

        return _orderedNodes = result.AsReadOnly();
    }
}
