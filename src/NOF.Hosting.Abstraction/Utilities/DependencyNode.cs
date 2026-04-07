using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

public sealed record DependencyNode(object ExtraInfo, IReadOnlyCollection<Type> AllInterfaces)
{
    public static IReadOnlyCollection<Type> CollectRelatedTypes<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TType>()
        => CollectRelatedTypes(typeof(TType));

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
