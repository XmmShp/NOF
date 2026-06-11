using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

/// <summary>
/// Ordered middleware types for one concrete pipeline.
/// Types can be added in arbitrary order and are frozen into dependency order on first execution.
/// </summary>
public class MessagePipelineTypes<TMiddlewareContract>
    where TMiddlewareContract : class, ITopologizable<TMiddlewareContract>
{
    private readonly List<Type> _registeredTypes = [];
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
        if (_registeredTypes.Contains(middlewareType))
        {
            return;
        }

        _registeredTypes.Add(middlewareType);
    }

    public void Freeze(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (_isFrozen)
        {
            return;
        }

        var middlewares = new List<TMiddlewareContract>(_registeredTypes.Count);
        foreach (var middlewareType in _registeredTypes)
        {
            middlewares.Add((TMiddlewareContract)services.GetRequiredService(middlewareType));
        }

        var graph = new DependencyGraph<TMiddlewareContract>(middlewares);
        var ordered = graph.GetExecutionOrder().Select(middleware => middleware.GetType()).ToList();

        _orderedTypes.Clear();
        _orderedTypes.AddRange(ordered);
        _isFrozen = true;
    }
}
