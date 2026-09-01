namespace NOF.Application;

/// <summary>
/// Declares a mapping from <typeparamref name="TSource"/> to <typeparamref name="TDestination"/>.
/// Place on a <c>partial static class</c>. The source generator will produce an assembly initializer
/// that registers all declared mappings into the global registry.
/// <para>
/// Multiple <c>[Mappable]</c> attributes can be placed on the same class, and the class
/// can be split across multiple files (partial). Each direction must be declared explicitly.
/// </para>
/// <para>
/// The generator will:
/// <list type="bullet">
///   <item>Match public writable properties (init or set) by name (case-insensitive).</item>
///   <item>Select the constructor with the most matched parameters (parameter name matched case-insensitively to source property names).</item>
///   <item>Provide first-class support for nullable values, collections, and <c>IValueObject&lt;T&gt;</c>.</item>
///   <item>Compose nested mappings into one queryable expression.</item>
///   <item>Report an error when a conversion cannot be represented as a mapping expression.</item>
/// </list>
/// </para>
/// </summary>
/// <typeparam name="TSource">The source type to map from.</typeparam>
/// <typeparam name="TDestination">The destination type to map to.</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class MappableAttribute<TSource, TDestination> : Attribute;
