namespace NOF.Hosting;

/// <summary>
/// Represents a directed acyclic graph (DAG) that supports topological sorting
/// based on explicit dependencies declared via the <see cref="IAfter{TDependency}"/> and <see cref="IBefore{TDependency}"/> interfaces.
/// This ensures nodes are executed in a valid order where dependencies run before their dependents.
/// </summary>
/// <typeparam name="T">The node type.</typeparam>
public class DependencyGraph<T>
{
    private readonly HashSet<DependencyNode<T>> _nodes;
    private IReadOnlyList<T>? _orderedNodes;

    // Maps a contract type (e.g., IDb, BaseConfig) to all nodes that implement/inherit it
    private readonly Dictionary<Type, HashSet<DependencyNode<T>>> _typeNodeMap;

    public DependencyGraph(IEnumerable<DependencyNode<T>> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        _typeNodeMap = [];
        var nodesSet = new HashSet<DependencyNode<T>>();

        foreach (var node in nodes)
        {
            if (!nodesSet.Add(node))
            {
                continue; // skip duplicates
            }
            IndexNode(node);
        }

        _nodes = nodesSet;
    }

    private void IndexNode(DependencyNode<T> node)
    {
        foreach (var relatedType in node.AllInterfaces.Where(ancestor => ancestor.IsAssignableTo(typeof(T))))
        {
            if (!_typeNodeMap.TryGetValue(relatedType, out var nodes))
            {
                nodes = [];
                _typeNodeMap[relatedType] = nodes;
            }
            nodes.Add(node);
        }
    }

    public IReadOnlyList<T> GetExecutionOrder()
    {
        if (_orderedNodes is not null)
        {
            return _orderedNodes;
        }

        var graph = _nodes.ToDictionary(n => n, _ => new HashSet<DependencyNode<T>>());
        var inDegree = _nodes.ToDictionary(n => n, _ => 0);

        foreach (var node in _nodes)
        {
            foreach (var iface in node.AllInterfaces)
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

        var queue = new Queue<DependencyNode<T>>(_nodes.Where(n => inDegree[n] == 0));
        var result = new List<T>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current.Instance);

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
