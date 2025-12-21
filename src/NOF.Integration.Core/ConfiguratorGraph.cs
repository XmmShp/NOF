namespace NOF;

/// <summary>
/// Represents a directed acyclic graph (DAG) of configurators that supports topological sorting
/// based on explicit dependencies declared via the <see cref="IAfter{TDependency}"/> and <see cref="IBefore{TDependency}"/> interfaces.
/// This ensures configurators are executed in a valid order where dependencies run before their dependents.
/// </summary>
/// <typeparam name="T">The type of configurator, which must implement <see cref="IConfig"/>.</typeparam>
internal class ConfiguratorGraph<T> where T : IConfig
{
    private readonly HashSet<T> _nodes;
    private IReadOnlyList<T>? _orderedConfigs;

    // Maps a contract type (e.g., IDb, BaseConfig) to all nodes that implement/inherit it
    private readonly Dictionary<Type, HashSet<T>> _typeNodeMap;

    public ConfiguratorGraph(IEnumerable<T> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        _typeNodeMap = [];
        var tasksSet = new HashSet<T>();

        foreach (var task in tasks)
        {
            if (!tasksSet.Add(task))
            {
                continue; // skip duplicates
            }
            IndexNode(task);
        }

        _nodes = tasksSet;
    }

    private void IndexNode(T node)
    {
        var type = node.GetType();
        var ancestors = new HashSet<Type>();

        // Collect all base types
        for (var t = type; t != null; t = t.BaseType)
        {
            ancestors.Add(t);
        }

        // Collect all interfaces
        foreach (var iface in type.GetInterfaces())
        {
            ancestors.Add(iface);
        }

        // Register node under every ancestor type (except root IConfig / T itself if undesired)
        foreach (var ancestor in ancestors.Where(ancestor => ancestor.IsAssignableTo(typeof(T))))
        {
            if (!_typeNodeMap.TryGetValue(ancestor, out var nodes))
            {
                nodes = [];
                _typeNodeMap[ancestor] = nodes;
            }
            nodes.Add(node);
        }
    }

    public IReadOnlyList<T> GetExecutionOrder()
    {
        if (_orderedConfigs is not null)
            return _orderedConfigs;

        var graph = _nodes.ToDictionary(n => n, _ => new HashSet<T>());
        var inDegree = _nodes.ToDictionary(n => n, _ => 0);

        foreach (var node in _nodes)
        {
            var nodeType = node.GetType();

            foreach (var iface in nodeType.GetInterfaces())
            {
                if (!iface.IsGenericType)
                {
                    continue;
                }

                var def = iface.GetGenericTypeDefinition();
                var contract = iface.GenericTypeArguments[0];

                if (def == typeof(IAfter<>))
                {
                    // node should run AFTER any node that provides 'contract'
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
                    // node should run BEFORE any node that provides 'contract'
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

        var queue = new Queue<T>(_nodes.Where(n => inDegree[n] == 0));
        var result = new List<T>();

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

        return _orderedConfigs = result.AsReadOnly();
    }
}