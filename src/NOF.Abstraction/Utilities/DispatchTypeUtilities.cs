using System.Diagnostics.CodeAnalysis;

namespace NOF.Abstraction;

public static class DispatchTypeUtilities
{
    /// <summary>
    /// Returns the type itself, all of its base types, and all implemented interfaces.
    /// </summary>
    public static Type[] GetSelfAndBaseTypesAndInterfaces(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var result = new List<Type>();
        var seenTypes = new HashSet<Type>();

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (seenTypes.Add(current))
            {
                result.Add(current);
            }
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            if (seenTypes.Add(interfaceType))
            {
                result.Add(interfaceType);
            }
        }

        return [.. result];
    }
}
