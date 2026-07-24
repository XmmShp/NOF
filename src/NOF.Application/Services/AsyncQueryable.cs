using NOF.Domain;
using System.Collections;
using System.ComponentModel;
using System.Linq.Expressions;

namespace NOF.Application;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IAsyncQueryableAccessor
{
    IQueryable Query { get; }
}

/// <summary>
/// Default wrapper that preserves asynchronous query execution metadata across LINQ composition.
/// </summary>
public class AsyncQueryable<TSource> : IAsyncQueryable<TSource>, IOrderedQueryable<TSource>, IAsyncQueryableAccessor
{
    private readonly IQueryable<TSource> _query;
    private readonly AsyncQueryProvider _provider;

    public AsyncQueryable(IQueryable<TSource> query, IAsyncQueryExecutor asyncExecutor)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(asyncExecutor);

        _query = query;
        AsyncExecutor = asyncExecutor;
        _provider = new AsyncQueryProvider(query.Provider, asyncExecutor);
    }

    public IAsyncQueryExecutor AsyncExecutor { get; }

    public Type ElementType => _query.ElementType;

    public Expression Expression => _query.Expression;

    public IQueryProvider Provider => _provider;

    IQueryable IAsyncQueryableAccessor.Query => _query;

    public IEnumerator<TSource> GetEnumerator()
        => _query.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
