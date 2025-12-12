namespace NOF;

/// <summary>
/// Represents a directed acyclic graph (DAG) of configurators that supports topological sorting
/// based on explicit dependencies declared via the <see cref="IDepsOn{T}"/> interface.
/// This ensures configurators are executed in a valid order where dependencies run before their dependents.
/// </summary>
/// <typeparam name="T">The type of configurator, which must implement <see cref="IConfigurator"/>.</typeparam>
internal class ConfiguratorGraph<T> where T : IConfigurator
{
    private readonly HashSet<T> _tasks;
    private readonly Dictionary<T, Type> _originType = [];
    private readonly Dictionary<Type, HashSet<T>> _typeTasks = [];

    /// <summary>
    /// Initializes a new instance of <see cref="ConfiguratorGraph{T}"/> with the specified collection of configurators.
    /// Duplicate instances are ignored (based on reference equality).
    /// Each configurator is indexed by its concrete runtime type to support dependency resolution.
    /// </summary>
    /// <param name="tasks">An enumerable configurator instances to include in the graph.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tasks"/> is null.</exception>
    public ConfiguratorGraph(IEnumerable<T> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        var tasksSet = new HashSet<T>();
        foreach (var task in tasks)
        {
            // Skip duplicates (HashSet handles reference equality by default for reference types)
            if (!tasksSet.Add(task))
            {
                continue;
            }

            var type = task.GetType();
            _originType[task] = type;
            _typeTasks.TryAdd(type, []);
            _typeTasks[type].Add(task);
        }

        _tasks = tasksSet;
    }

    /// <summary>
    /// Computes and returns the execution order of configurators using topological sorting.
    /// Configurators with no dependencies are scheduled first.
    /// The order guarantees that for any dependency relationship A → B (B depends on A),
    /// A appears before B in the result list.
    /// </summary>
    /// <returns>A list of configurators in valid execution order.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a circular dependency is detected among the configurators.
    /// </exception>
    public List<T> GetExecutionOrder()
    {
        // Step 1: Build the dependency graph (dependency → dependent)
        var graph = _tasks.ToDictionary(t => t, _ => new HashSet<T>());
        var inDegree = _tasks.ToDictionary(t => t, _ => 0);

        foreach (var task in _tasks)
        {
            var dependencies = GetConcreteDependencies(task);
            foreach (var dependency in dependencies)
            {
                // Edge: dependency -> task (task depends on dependency)
                if (graph.TryGetValue(dependency, out var dependents))
                {
                    dependents.Add(task);
                    inDegree[task]++;
                }
            }
        }

        // Step 2: Initialize queue with all nodes having zero in-degree (no dependencies)
        var queue = new Queue<T>(_tasks.Where(t => inDegree[t] == 0));
        var result = new List<T>();

        // Step 3: Perform Kahn's algorithm for topological sort
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var dependent in graph[current])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        // Step 4: Detect cycles
        if (result.Count != _tasks.Count)
        {
            throw new InvalidOperationException("Circular dependency detected among configurators. Please ensure that dependency chains declared via IDepsOn<> do not form cycles.");
        }

        return result;
    }

    /// <summary>
    /// Resolves the actual configurator instances that the given <paramref name="task"/> depends on,
    /// by inspecting its implementation of <see cref="IDepsOn{TDeps}"/> interfaces.
    /// Only dependencies of type compatible with <typeparamref name="T"/> are considered.
    /// </summary>
    /// <param name="task">The configurator whose dependencies to resolve.</param>
    /// <returns>An enumerable of concrete dependency configurator instances.</returns>
    private IEnumerable<T> GetConcreteDependencies(T task)
    {
        var taskType = _originType[task];

        // Inspect all interfaces implemented by the task
        foreach (var iface in taskType.GetInterfaces())
        {
            // Check if it's a generic IDepsOn<> interface
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDepsOn<>))
            {
                var dependencyType = iface.GenericTypeArguments[0];

                // Only consider dependencies that are assignable to T (i.e., valid configurators in this graph)
                if (!typeof(T).IsAssignableFrom(dependencyType))
                {
                    continue;
                }

                // Find all registered configurator instances whose concrete type is assignable from the dependency type
                var implementingTasks = _originType.Values
                    .Where(concreteType => dependencyType.IsAssignableFrom(concreteType))
                    .SelectMany(concreteType => _typeTasks[concreteType])
                    .Distinct();

                foreach (var depTask in implementingTasks)
                {
                    yield return depTask;
                }
            }
        }
    }
}