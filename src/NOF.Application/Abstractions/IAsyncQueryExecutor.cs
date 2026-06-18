using System.Linq.Expressions;

namespace NOF.Application;

/// <summary>
/// Executes asynchronous terminal operations for a LINQ query.
/// Concrete data providers decide whether execution is truly asynchronous or falls back to synchronous evaluation.
/// </summary>
public interface IAsyncQueryExecutor
{
    IAsyncEnumerable<TSource> AsAsyncEnumerable<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);

    Task<List<TSource>> ToListAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);
    Task<TSource[]> ToArrayAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);
    Task LoadAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);
    Task<bool> AllAsync<TSource>(IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default);
    Task<bool> ContainsAsync<TSource>(IQueryable<TSource> source, TSource value, CancellationToken cancellationToken = default);

    Task<int> CountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);
    Task<long> LongCountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);

    Task<TSource> FirstAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);
    Task<TSource?> FirstOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);
    Task<TSource> SingleAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);
    Task<TSource?> SingleOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);
    Task<TSource> LastAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);
    Task<TSource?> LastOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);
    Task<TSource> ElementAtAsync<TSource>(IQueryable<TSource> source, int index, CancellationToken cancellationToken = default);
    Task<TSource?> ElementAtOrDefaultAsync<TSource>(IQueryable<TSource> source, int index, CancellationToken cancellationToken = default);

    Task<TSource> MinAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);
    Task<TSource> MaxAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default);

    Task<int> SumAsync(IQueryable<int> source, CancellationToken cancellationToken = default);
    Task<int?> SumAsync(IQueryable<int?> source, CancellationToken cancellationToken = default);
    Task<long> SumAsync(IQueryable<long> source, CancellationToken cancellationToken = default);
    Task<long?> SumAsync(IQueryable<long?> source, CancellationToken cancellationToken = default);
    Task<float> SumAsync(IQueryable<float> source, CancellationToken cancellationToken = default);
    Task<float?> SumAsync(IQueryable<float?> source, CancellationToken cancellationToken = default);
    Task<double> SumAsync(IQueryable<double> source, CancellationToken cancellationToken = default);
    Task<double?> SumAsync(IQueryable<double?> source, CancellationToken cancellationToken = default);
    Task<decimal> SumAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default);
    Task<decimal?> SumAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default);

    Task<double> AverageAsync(IQueryable<int> source, CancellationToken cancellationToken = default);
    Task<double?> AverageAsync(IQueryable<int?> source, CancellationToken cancellationToken = default);
    Task<double> AverageAsync(IQueryable<long> source, CancellationToken cancellationToken = default);
    Task<double?> AverageAsync(IQueryable<long?> source, CancellationToken cancellationToken = default);
    Task<float> AverageAsync(IQueryable<float> source, CancellationToken cancellationToken = default);
    Task<float?> AverageAsync(IQueryable<float?> source, CancellationToken cancellationToken = default);
    Task<double> AverageAsync(IQueryable<double> source, CancellationToken cancellationToken = default);
    Task<double?> AverageAsync(IQueryable<double?> source, CancellationToken cancellationToken = default);
    Task<decimal> AverageAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default);
    Task<decimal?> AverageAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default);
}
