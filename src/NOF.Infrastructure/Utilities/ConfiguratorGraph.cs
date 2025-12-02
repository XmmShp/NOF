namespace NOF;

public class ConfiguratorGraph<T> where T : IConfigurator
{
    private readonly HashSet<T> _tasks;
    private readonly Dictionary<T, Type> _originType = [];
    private readonly Dictionary<Type, HashSet<T>> _typeTasks = [];

    public ConfiguratorGraph(IEnumerable<T> tasks)
    {
        var tasksSet = new HashSet<T>();
        foreach (var task in tasks)
        {
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
    /// 返回按拓扑顺序排列的任务列表（无依赖的任务在前）
    /// </summary>
    public List<T> GetExecutionOrder()
    {
        // 步骤 1: 构建依赖图（父任务 -> 子任务）
        var graph = _tasks.ToDictionary(t => t, _ => new HashSet<T>());
        var inDegree = _tasks.ToDictionary(t => t, _ => 0);
        foreach (var task in _tasks)
        {
            var dependencies = GetConcreteDependencies(task);
            foreach (var dependency in dependencies)
            {
                graph[dependency].Add(task);
                inDegree[task]++;
            }
        }

        var queue = new Queue<T>(_tasks.Where(t => inDegree[t] == 0));
        var result = new List<T>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var dest in graph[current])
            {
                inDegree[dest]--;
                if (inDegree[dest] == 0)
                {
                    queue.Enqueue(dest);
                }
            }
        }

        if (result.Count != _tasks.Count)
        {
            throw new InvalidOperationException(
                "Circular dependency detected among startup tasks. " +
                "Please check your IDepsOn<> declarations."
            );
        }

        return result;
    }

    private IEnumerable<T> GetConcreteDependencies(T task)
    {
        var taskType = _originType[task];

        // 遍历所有接口，找出 IDepsOn<T>
        foreach (var iface in taskType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDepsOn<>))
            {
                var dependencyType = iface.GenericTypeArguments[0];
                if (!dependencyType.IsAssignableTo(typeof(T)))
                {
                    continue;
                }

                // 找到所有已注册的、实现了该依赖接口的具体任务
                var implementingTasks = _originType.Values
                    .Where(t => dependencyType.IsAssignableFrom(t))
                    .SelectMany(t => _typeTasks[t])
                    .Distinct();

                foreach (var depTask in implementingTasks)
                {
                    yield return depTask;
                }
            }
        }
    }
}