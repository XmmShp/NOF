using System.Linq.Expressions;

namespace NOF;

public static class QueryableExtensions
{
    extension<T>(IQueryable<T> source)
    {
        public IQueryable<T> WhereIf(Expression<Func<T, bool>> predicate, bool condition)
        {
            return condition ? source.Where(predicate) : source;
        }
        public IQueryable<T> WhereIf(Expression<Func<T, int, bool>> predicate, bool condition)
        {
            return condition ? source.Where(predicate) : source;
        }
    }
}
