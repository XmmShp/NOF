using System.Linq.Expressions;

namespace NOF.Application;

/// <summary>
/// Identifies a mapping by its closed source type, destination type, and optional name.
/// </summary>
/// <param name="Source">The source type.</param>
/// <param name="Destination">The destination type.</param>
/// <param name="Name">Optional mapping name. <see langword="null"/> = default (unnamed) mapping.</param>
public sealed record MapKey(Type Source, Type Destination, string? Name = null);

/// <summary>
/// Registers the expression that defines one mapping.
/// The same expression is used for query projection and in-memory mapping.
/// </summary>
public sealed record MappingRegistration
{
    private readonly Func<IQueryable, LambdaExpression, IQueryable> _project;

    private MappingRegistration(
        MapKey key,
        LambdaExpression expression,
        Func<IQueryable, LambdaExpression, IQueryable> project)
    {
        Key = key;
        Expression = expression;
        _project = project;
    }

    public MapKey Key { get; }

    public LambdaExpression Expression { get; }

    public static MappingRegistration Of<TSource, TDestination>(
        Expression<Func<TSource, TDestination>> expression,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new MappingRegistration(
            new MapKey(typeof(TSource), typeof(TDestination), name),
            expression,
            static (source, mapping) => ((IQueryable<TSource>)source)
                .Select((Expression<Func<TSource, TDestination>>)mapping));
    }

    internal IQueryable Project(IQueryable source, LambdaExpression expression)
        => _project(source, expression);
}
