namespace NOF.Hosting;

/// <summary>
/// Represents a directed acyclic graph (DAG) that supports topological sorting
/// based on explicit dependencies declared via <see cref="ITopologizable{TContract}"/> runtime metadata.
/// This ensures nodes are executed in a valid order where dependencies run before their dependents.
/// </summary>
public sealed class DependencyGraph<T>
    where T : class, ITopologizable<T>
{
    private readonly IReadOnlyList<T> _nodes;
    private IReadOnlyList<T>? _orderedNodes;

    public DependencyGraph(IEnumerable<T> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        _nodes = [.. new HashSet<T>(nodes)];
    }

    public IReadOnlyList<T> GetExecutionOrder()
    {
        if (_orderedNodes is not null)
        {
            return _orderedNodes;
        }

        var graph = _nodes.ToDictionary(n => n, _ => new HashSet<T>());
        var inDegree = _nodes.ToDictionary(n => n, _ => 0);

        for (var i = 0; i < _nodes.Count; i++)
        {
            var current = _nodes[i];
            for (var j = i + 1; j < _nodes.Count; j++)
            {
                var other = _nodes[j];

                AddEdge(graph, inDegree, current, other, current.Compare(other));
                AddEdge(graph, inDegree, other, current, other.Compare(current));
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
                "Please ensure that dependency chains declared via ITopologizable<> do not form cycles.");
        }

        return _orderedNodes = result.AsReadOnly();
    }

    private static void AddEdge(
        Dictionary<T, HashSet<T>> graph,
        Dictionary<T, int> inDegree,
        T current,
        T other,
        TopologyComparison comparison)
    {
        switch (comparison)
        {
            case TopologyComparison.Before:
                if (graph[current].Add(other))
                {
                    inDegree[other]++;
                }
                break;
            case TopologyComparison.After:
                if (graph[other].Add(current))
                {
                    inDegree[current]++;
                }
                break;
            case TopologyComparison.DoesNotMatter:
            default:
                break;
        }
    }
}
