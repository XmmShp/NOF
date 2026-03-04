namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Provides a deterministic, safe endpoint name from a <see cref="Type"/>.
/// </summary>
public static class EndpointNameHelper
{
    /// <summary>
    /// Produces a stable, safe endpoint name from a type's full name.
    /// Non-generic types: <c>Namespace_TypeName</c> (dots → underscores, nested <c>+</c> → <c>____</c>).
    /// Generic types: <c>OpenName__Arg1___Arg2</c>.
    /// </summary>
    public static string BuildSafeTypeName(Type type)
    {
        if (!type.IsGenericType)
        {
            return (type.FullName ?? type.Name).Replace('.', '_').Replace("+", "____");
        }

        var defName = (type.GetGenericTypeDefinition().FullName ?? type.GetGenericTypeDefinition().Name);
        var backtickIndex = defName.LastIndexOf('`');
        if (backtickIndex >= 0)
        {
            defName = defName[..backtickIndex];
        }
        defName = defName.Replace('.', '_').Replace("+", "____");

        var args = type.GetGenericArguments();
        var argNames = new string[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            argNames[i] = BuildSafeTypeName(args[i]);
        }

        return $"{defName}__{string.Join("___", argNames)}";
    }
}
