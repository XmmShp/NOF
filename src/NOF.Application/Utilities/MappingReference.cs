using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Marks a nested mapping inside a registered expression template.
/// References are expanded before a mapping expression is exposed or executed.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class MappingReference
{
    public static TDestination Map<TSource, TDestination>(TSource source, string? name = null)
        => throw new InvalidOperationException(
            "MappingReference.Map can only be used inside a registered mapping expression.");
}
