using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

public sealed record DependencyNode<T>(T Instance, IReadOnlyCollection<Type> AllInterfaces)
{
    public static DependencyNode<TStep> Create<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep instance)
        where TStep : T
    {
        ArgumentNullException.ThrowIfNull(instance);
        return new DependencyNode<TStep>(instance, CollectRelatedTypes<TStep>());
    }

    public static IReadOnlyCollection<Type> CollectRelatedTypes<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>()
        => CollectRelatedTypes(typeof(TStep));

    public static IReadOnlyCollection<Type> CollectRelatedTypes([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var related = new HashSet<Type>();
        for (var current = type; current is not null; current = current.BaseType)
        {
            related.Add(current);
        }

        foreach (var iface in type.GetInterfaces())
        {
            related.Add(iface);
        }

        return related.ToArray();
    }
}
