using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NOF.Application;

public sealed class InMemoryAsyncQueryExecutor : IAsyncQueryExecutor
{
    public static InMemoryAsyncQueryExecutor Instance { get; } = new();

    private InMemoryAsyncQueryExecutor() { }

    public IAsyncEnumerable<TSource> AsAsyncEnumerable<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is IAsyncEnumerable<TSource> asyncSource)
        {
            return asyncSource;
        }

        return EnumerateSync(source, cancellationToken);
    }

    public Task<int> ExecuteDeleteAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("The current query provider does not support set-based delete operations.");
    }

    public Task<int> ExecuteUpdateAsync<TSource>(IQueryable<TSource> source, IUpdateSetters<TSource> setters, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(setters);
        throw new NotSupportedException("The current query provider does not support set-based update operations.");
    }

    public async Task<List<TSource>> ToListAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        var result = new List<TSource>();
        await foreach (var item in AsAsyncEnumerable(source, cancellationToken).WithCancellation(cancellationToken))
        {
            result.Add(item);
        }

        return result;
    }

    public async Task<TSource[]> ToArrayAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => [.. await ToListAsync(source, cancellationToken)];

    public async Task LoadAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        await foreach (var _ in AsAsyncEnumerable(source, cancellationToken).WithCancellation(cancellationToken))
        {
        }
    }

    public Task<bool> AnyAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.Any());
    }

    public Task<bool> AllAsync<TSource>(IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.All(predicate));
    }

    public Task<bool> ContainsAsync<TSource>(IQueryable<TSource> source, TSource value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.Contains(value));
    }

    public Task<int> CountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.Count());
    }

    public Task<long> LongCountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.LongCount());
    }

    public Task<TSource> FirstAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.First());
    }

    public Task<TSource?> FirstOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.FirstOrDefault());
    }

    public Task<TSource> SingleAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.Single());
    }

    public Task<TSource?> SingleOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.SingleOrDefault());
    }

    public Task<TSource> LastAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.Last());
    }

    public Task<TSource?> LastOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.LastOrDefault());
    }

    public Task<TSource> ElementAtAsync<TSource>(IQueryable<TSource> source, int index, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.ElementAt(index));
    }

    public Task<TSource?> ElementAtOrDefaultAsync<TSource>(IQueryable<TSource> source, int index, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.ElementAtOrDefault(index));
    }

    public Task<TSource> MinAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.Min()!);
    }

    public Task<TSource> MaxAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(source.Max()!);
    }

    public Task<int> SumAsync(IQueryable<int> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Sum, cancellationToken);
    public Task<int?> SumAsync(IQueryable<int?> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Sum, cancellationToken);
    public Task<long> SumAsync(IQueryable<long> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Sum, cancellationToken);
    public Task<long?> SumAsync(IQueryable<long?> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Sum, cancellationToken);
    public Task<float> SumAsync(IQueryable<float> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Sum, cancellationToken);
    public Task<float?> SumAsync(IQueryable<float?> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Sum, cancellationToken);
    public Task<double> SumAsync(IQueryable<double> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Sum, cancellationToken);
    public Task<double?> SumAsync(IQueryable<double?> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Sum, cancellationToken);
    public Task<decimal> SumAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Sum, cancellationToken);
    public Task<decimal?> SumAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Sum, cancellationToken);

    public Task<double> AverageAsync(IQueryable<int> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Average, cancellationToken);
    public Task<double?> AverageAsync(IQueryable<int?> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Average, cancellationToken);
    public Task<double> AverageAsync(IQueryable<long> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Average, cancellationToken);
    public Task<double?> AverageAsync(IQueryable<long?> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Average, cancellationToken);
    public Task<float> AverageAsync(IQueryable<float> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Average, cancellationToken);
    public Task<float?> AverageAsync(IQueryable<float?> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Average, cancellationToken);
    public Task<double> AverageAsync(IQueryable<double> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Average, cancellationToken);
    public Task<double?> AverageAsync(IQueryable<double?> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Average, cancellationToken);
    public Task<decimal> AverageAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Average, cancellationToken);
    public Task<decimal?> AverageAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default) => FromSync(source, Queryable.Average, cancellationToken);

    private static Task<TResult> FromSync<TSource, TResult>(IQueryable<TSource> source, Func<IQueryable<TSource>, TResult> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(operation(source));
    }

    private static async IAsyncEnumerable<TSource> EnumerateSync<TSource>(
        IEnumerable<TSource> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await Task.CompletedTask;
        }
    }
}
