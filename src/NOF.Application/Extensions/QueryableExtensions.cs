using NOF.Application;
using System.Linq.Expressions;

namespace System.Linq;

/// <summary>
/// Provider-agnostic asynchronous query helpers for application-layer LINQ.
/// Queries execute through the current <see cref="IAsyncQueryExecutor"/> when available.
/// </summary>
public static class QueryableAsyncExtensions
{
    extension<TSource>(IQueryable<TSource> source)
    {
        public IAsyncQueryable<TSource> AsAsyncQueryable()
        {
            ArgumentNullException.ThrowIfNull(source);
            return source as IAsyncQueryable<TSource> ?? new AsyncQueryable<TSource>(source, InMemoryAsyncQueryExecutor.Instance);
        }

        public IAsyncEnumerable<TSource> AsAsyncEnumerable(
            CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.AsAsyncEnumerable(source, cancellationToken);

        public Task<int> ExecuteDeleteAsync(
            CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.ExecuteDeleteAsync(source, cancellationToken);

        public Task<int> ExecuteUpdateAsync(
            Func<UpdateSetters<TSource>, UpdateSetters<TSource>> setPropertyCalls,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(setPropertyCalls);

            var setters = setPropertyCalls(new UpdateSetters<TSource>())
                ?? throw new InvalidOperationException("The batch update setter builder cannot be null.");

            return source.AsAsyncQueryable().AsyncExecutor.ExecuteUpdateAsync(source, setters, cancellationToken);
        }

        public Task<List<TSource>> ToListAsync(
            CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.ToListAsync(source, cancellationToken);

        public Task<TSource[]> ToArrayAsync(
            CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.ToArrayAsync(source, cancellationToken);

        public async Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TKey, TElement>(
            Func<TSource, TKey> keySelector,
            Func<TSource, TElement> elementSelector,
            IEqualityComparer<TKey>? comparer = null,
            CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(keySelector);
            ArgumentNullException.ThrowIfNull(elementSelector);

            var list = await source.ToListAsync(cancellationToken);
            return list.ToDictionary(keySelector, elementSelector, comparer);
        }

        public Task<Dictionary<TKey, TSource>> ToDictionaryAsync<TKey>(
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey>? comparer = null,
            CancellationToken cancellationToken = default)
            where TKey : notnull
            => source.ToDictionaryAsync(keySelector, static value => value, comparer, cancellationToken);

        public async Task<HashSet<TSource>> ToHashSetAsync(
            IEqualityComparer<TSource>? comparer = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);

            var list = await source.ToListAsync(cancellationToken);
            return list.ToHashSet(comparer);
        }

        public Task LoadAsync(
            CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.LoadAsync(source, cancellationToken);

        public async Task ForEachAsync(
            Action<TSource> action,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(action);

            await foreach (var item in source.AsAsyncEnumerable(cancellationToken).WithCancellation(cancellationToken))
            {
                action(item);
            }
        }

        public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.AnyAsync(source, cancellationToken);

        public Task<bool> AnyAsync(
            Expression<Func<TSource, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            return source.Where(predicate).AnyAsync(cancellationToken);
        }

        public Task<bool> AllAsync(
            Expression<Func<TSource, bool>> predicate,
            CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.AllAsync(source, predicate, cancellationToken);

        public Task<bool> ContainsAsync(
            TSource value,
            CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.ContainsAsync(source, value, cancellationToken);

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.CountAsync(source, cancellationToken);

        public Task<int> CountAsync(
            Expression<Func<TSource, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            return source.Where(predicate).CountAsync(cancellationToken);
        }

        public Task<long> LongCountAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.LongCountAsync(source, cancellationToken);

        public Task<long> LongCountAsync(
            Expression<Func<TSource, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            return source.Where(predicate).LongCountAsync(cancellationToken);
        }

        public Task<TSource> FirstAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.FirstAsync(source, cancellationToken);

        public Task<TSource> FirstAsync(
            Expression<Func<TSource, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            return source.Where(predicate).FirstAsync(cancellationToken);
        }

        public Task<TSource?> FirstOrDefaultAsync(
            CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.FirstOrDefaultAsync(source, cancellationToken);

        public Task<TSource?> FirstOrDefaultAsync(
            Expression<Func<TSource, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            return source.Where(predicate).FirstOrDefaultAsync(cancellationToken);
        }

        public Task<TSource> SingleAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.SingleAsync(source, cancellationToken);

        public Task<TSource> SingleAsync(
            Expression<Func<TSource, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            return source.Where(predicate).SingleAsync(cancellationToken);
        }

        public Task<TSource?> SingleOrDefaultAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.SingleOrDefaultAsync(source, cancellationToken);

        public Task<TSource?> SingleOrDefaultAsync(
            Expression<Func<TSource, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            return source.Where(predicate).SingleOrDefaultAsync(cancellationToken);
        }

        public Task<TSource> LastAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.LastAsync(source, cancellationToken);

        public Task<TSource> LastAsync(
            Expression<Func<TSource, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            return source.Where(predicate).LastAsync(cancellationToken);
        }

        public Task<TSource?> LastOrDefaultAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.LastOrDefaultAsync(source, cancellationToken);

        public Task<TSource?> LastOrDefaultAsync(
            Expression<Func<TSource, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            return source.Where(predicate).LastOrDefaultAsync(cancellationToken);
        }

        public Task<TSource> ElementAtAsync(int index, CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.ElementAtAsync(source, index, cancellationToken);

        public Task<TSource?> ElementAtOrDefaultAsync(int index, CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.ElementAtOrDefaultAsync(source, index, cancellationToken);

        public Task<TSource> MinAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.MinAsync(source, cancellationToken);

        public Task<TResult> MinAsync<TResult>(
            Expression<Func<TSource, TResult>> selector,
            CancellationToken cancellationToken = default)
            => source.Select(selector).MinAsync(cancellationToken);

        public Task<TSource> MaxAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.MaxAsync(source, cancellationToken);

        public Task<TResult> MaxAsync<TResult>(
            Expression<Func<TSource, TResult>> selector,
            CancellationToken cancellationToken = default)
            => source.Select(selector).MaxAsync(cancellationToken);

        public Task<int> SumAsync(Expression<Func<TSource, int>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).SumAsync(cancellationToken);

        public Task<int?> SumAsync(Expression<Func<TSource, int?>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).SumAsync(cancellationToken);

        public Task<long> SumAsync(Expression<Func<TSource, long>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).SumAsync(cancellationToken);

        public Task<long?> SumAsync(Expression<Func<TSource, long?>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).SumAsync(cancellationToken);

        public Task<float> SumAsync(Expression<Func<TSource, float>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).SumAsync(cancellationToken);

        public Task<float?> SumAsync(Expression<Func<TSource, float?>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).SumAsync(cancellationToken);

        public Task<double> SumAsync(Expression<Func<TSource, double>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).SumAsync(cancellationToken);

        public Task<double?> SumAsync(Expression<Func<TSource, double?>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).SumAsync(cancellationToken);

        public Task<decimal> SumAsync(Expression<Func<TSource, decimal>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).SumAsync(cancellationToken);

        public Task<decimal?> SumAsync(Expression<Func<TSource, decimal?>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).SumAsync(cancellationToken);

        public Task<double> AverageAsync(Expression<Func<TSource, int>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).AverageAsync(cancellationToken);

        public Task<double?> AverageAsync(Expression<Func<TSource, int?>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).AverageAsync(cancellationToken);

        public Task<double> AverageAsync(Expression<Func<TSource, long>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).AverageAsync(cancellationToken);

        public Task<double?> AverageAsync(Expression<Func<TSource, long?>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).AverageAsync(cancellationToken);

        public Task<float> AverageAsync(Expression<Func<TSource, float>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).AverageAsync(cancellationToken);

        public Task<float?> AverageAsync(Expression<Func<TSource, float?>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).AverageAsync(cancellationToken);

        public Task<double> AverageAsync(Expression<Func<TSource, double>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).AverageAsync(cancellationToken);

        public Task<double?> AverageAsync(Expression<Func<TSource, double?>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).AverageAsync(cancellationToken);

        public Task<decimal> AverageAsync(Expression<Func<TSource, decimal>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).AverageAsync(cancellationToken);

        public Task<decimal?> AverageAsync(Expression<Func<TSource, decimal?>> selector, CancellationToken cancellationToken = default)
            => source.Select(selector).AverageAsync(cancellationToken);
    }

    extension(IQueryable<int> source)
    {
        public Task<int> SumAsync(CancellationToken cancellationToken = default)
        => source.AsAsyncQueryable().AsyncExecutor.SumAsync(source, cancellationToken);

        public Task<double> AverageAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.AverageAsync(source, cancellationToken);
    }

    extension(IQueryable<int?> source)
    {
        public Task<int?> SumAsync(CancellationToken cancellationToken = default)
        => source.AsAsyncQueryable().AsyncExecutor.SumAsync(source, cancellationToken);

        public Task<double?> AverageAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.AverageAsync(source, cancellationToken);
    }

    extension(IQueryable<long> source)
    {
        public Task<long> SumAsync(CancellationToken cancellationToken = default)
        => source.AsAsyncQueryable().AsyncExecutor.SumAsync(source, cancellationToken);

        public Task<double> AverageAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.AverageAsync(source, cancellationToken);
    }

    extension(IQueryable<long?> source)
    {
        public Task<long?> SumAsync(CancellationToken cancellationToken = default)
        => source.AsAsyncQueryable().AsyncExecutor.SumAsync(source, cancellationToken);

        public Task<double?> AverageAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.AverageAsync(source, cancellationToken);
    }

    extension(IQueryable<float> source)
    {
        public Task<float> SumAsync(CancellationToken cancellationToken = default)
        => source.AsAsyncQueryable().AsyncExecutor.SumAsync(source, cancellationToken);

        public Task<float> AverageAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.AverageAsync(source, cancellationToken);
    }

    extension(IQueryable<float?> source)
    {
        public Task<float?> SumAsync(CancellationToken cancellationToken = default)
        => source.AsAsyncQueryable().AsyncExecutor.SumAsync(source, cancellationToken);

        public Task<float?> AverageAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.AverageAsync(source, cancellationToken);
    }

    extension(IQueryable<double> source)
    {
        public Task<double> SumAsync(CancellationToken cancellationToken = default)
        => source.AsAsyncQueryable().AsyncExecutor.SumAsync(source, cancellationToken);

        public Task<double> AverageAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.AverageAsync(source, cancellationToken);
    }

    extension(IQueryable<double?> source)
    {
        public Task<double?> SumAsync(CancellationToken cancellationToken = default)
        => source.AsAsyncQueryable().AsyncExecutor.SumAsync(source, cancellationToken);

        public Task<double?> AverageAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.AverageAsync(source, cancellationToken);
    }

    extension(IQueryable<decimal> source)
    {
        public Task<decimal> SumAsync(CancellationToken cancellationToken = default)
        => source.AsAsyncQueryable().AsyncExecutor.SumAsync(source, cancellationToken);

        public Task<decimal> AverageAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.AverageAsync(source, cancellationToken);
    }

    extension(IQueryable<decimal?> source)
    {
        public Task<decimal?> SumAsync(CancellationToken cancellationToken = default)
        => source.AsAsyncQueryable().AsyncExecutor.SumAsync(source, cancellationToken);

        public Task<decimal?> AverageAsync(CancellationToken cancellationToken = default)
            => source.AsAsyncQueryable().AsyncExecutor.AverageAsync(source, cancellationToken);
    }
}
