using System.Linq.Expressions;

namespace NOF.Application;

/// <summary>
/// Resolves one expression-based mapping definition for both query projection and in-memory mapping.
/// </summary>
public interface IMapper
{
    /// <summary>
    /// Gets the fully expanded mapping expression for the requested closed type pair.
    /// </summary>
    Expression<Func<TSource, TDestination>> GetExpression<TSource, TDestination>(string? name = null);

    /// <summary>
    /// Projects an untyped query by applying the registered expression for its element type.
    /// </summary>
    IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, string? name = null);

    /// <summary>
    /// Maps one value by compiling and caching the same expression returned by
    /// <see cref="GetExpression{TSource, TDestination}(string?)"/>.
    /// </summary>
    TDestination Map<TSource, TDestination>(TSource source, string? name = null);
}
