using System.Diagnostics.CodeAnalysis;

namespace NOF.Abstraction;

public static partial class NOFAbstractionExtensions
{
    extension(Type type)
    {
        public string DisplayName => type.FullName ?? type.Name;
    }

    extension([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
    {
        public Type[] GetAllAssignableTypes()
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
}
