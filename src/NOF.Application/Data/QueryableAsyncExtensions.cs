using System.Linq.Expressions;

namespace NOF.Application.Data;

/// <summary>
/// Provider-agnostic asynchronous query helpers for application-layer LINQ.
/// </summary>
public static class QueryableAsyncExtensions
{
    public static async Task<List<TSource>> ToListAsync<TSource>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is IAsyncEnumerable<TSource> asyncSource)
        {
            var result = new List<TSource>();
            await foreach (var item in asyncSource.WithCancellation(cancellationToken))
            {
                result.Add(item);
            }

            return result;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return source.ToList();
    }

    public static Task<TSource?> FirstOrDefaultAsync<TSource>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return source.Where(predicate).FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<TSource?> FirstOrDefaultAsync<TSource>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is IAsyncEnumerable<TSource> asyncSource)
        {
            await foreach (var item in asyncSource.WithCancellation(cancellationToken))
            {
                return item;
            }

            return default;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return source.FirstOrDefault();
    }

    public static Task<bool> AnyAsync<TSource>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return source.Where(predicate).AnyAsync(cancellationToken);
    }

    public static async Task<bool> AnyAsync<TSource>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is IAsyncEnumerable<TSource> asyncSource)
        {
            await foreach (var _ in asyncSource.WithCancellation(cancellationToken))
            {
                return true;
            }

            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return source.Any();
    }
}
