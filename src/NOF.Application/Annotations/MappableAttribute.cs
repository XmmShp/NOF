namespace NOF.Application;

/// <summary>
/// Declares a mapping from <typeparamref name="TSource"/> to <typeparamref name="TDestination"/>.
/// Place on a <c>partial static class</c>. The source generator will produce an assembly initializer
/// that registers all declared mappings into the global <see cref="MapperRegistry"/>.
/// <para>
/// Multiple <c>[Mappable]</c> attributes can be placed on the same class, and the class
/// can be split across multiple files (partial). The generator merges them into one method.
/// </para>
/// <para>
/// The generator will:
/// <list type="bullet">
///   <item>Match public writable properties (init or set) by name (case-insensitive).</item>
///   <item>Select the constructor with the most matched parameters (parameter name matched case-insensitively to source property names).</item>
///   <item>Provide first-class support for <c>Optional&lt;T&gt;</c>, <c>Result&lt;T&gt;</c>, and <c>IValueObject&lt;T&gt;</c>.</item>
///   <item>Provide built-in support for common type conversions (string↔int, int↔enum, enum↔string, etc.).</item>
///   <item>Use <see cref="IMapper"/> for all other property type conversions.</item>
/// </list>
/// </para>
/// </summary>
/// <typeparam name="TSource">The source type to map from.</typeparam>
/// <typeparam name="TDestination">The destination type to map to.</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class MappableAttribute<TSource, TDestination> : Attribute
{
    /// <summary>
    /// When <see langword="true"/>, the generator will also produce the reverse mapping
    /// (<typeparamref name="TDestination"/> → <typeparamref name="TSource"/>).
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool TwoWay { get; set; }
}

/// <summary>
/// Declares a mapping from the annotated type's first generic type argument to the specified <see cref="Type"/>.
/// This is the non-generic overload for scenarios where the target type cannot be used as a generic type argument.
/// <para>Place on a <c>partial static class</c>. See <see cref="MappableAttribute{TSource, TDestination}"/> for details.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class MappableAttribute : Attribute
{
    /// <summary>
    /// The source type to map from.
    /// </summary>
    public Type Source { get; }

    /// <summary>
    /// The destination type to map to.
    /// </summary>
    public Type Destination { get; }

    /// <summary>
    /// Creates a new <see cref="MappableAttribute"/> for the given source → destination pair.
    /// </summary>
    /// <param name="source">The source type.</param>
    /// <param name="destination">The destination type.</param>
    public MappableAttribute(Type source, Type destination)
    {
        Source = source;
        Destination = destination;
    }

    /// <summary>
    /// When <see langword="true"/>, the generator will also produce the reverse mapping.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool TwoWay { get; set; }
}
