using NOF.Domain;
using System.Linq.Expressions;

namespace NOF.Application;

public sealed class AsyncQueryProvider(IQueryProvider innerProvider, IAsyncQueryExecutor asyncExecutor) : IQueryProvider
{
    private readonly IQueryProvider _innerProvider = innerProvider;
    private readonly IAsyncQueryExecutor _asyncExecutor = asyncExecutor;

    public IQueryable CreateQuery(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return _innerProvider.CreateQuery(expression);
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return new AsyncQueryable<TElement>(_innerProvider.CreateQuery<TElement>(expression), _asyncExecutor);
    }

    public object? Execute(Expression expression)
        => _innerProvider.Execute(expression);

    public TResult Execute<TResult>(Expression expression)
        => _innerProvider.Execute<TResult>(expression);
}
