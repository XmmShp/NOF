using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal static class SoftDeleteModelHelper
{
    public const string DeletedAtUnixTimePropertyName = "__DeletedAtUnixTime";
    public const long ActiveDeletedAtUnixTime = 0;

    public static bool ShouldConfigureSoftDelete(IMutableEntityType entityType)
        => entityType.BaseType is null
            && !entityType.IsOwned()
            && entityType.ClrType != typeof(Dictionary<string, object>);

    [RequiresDynamicCode("Calls System.Linq.Expressions.Expression.Lambda(Expression, params ParameterExpression[])")]
    [RequiresUnreferencedCode("Calls System.Linq.Expressions.Expression.Property(Expression, String)")]
    public static LambdaExpression BuildSoftDeleteFilter(Type entityClrType)
    {
        var entityParameter = Expression.Parameter(entityClrType, "entity");
        var deletedAtUnixTimeProperty = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [typeof(long)],
            entityParameter,
            Expression.Constant(DeletedAtUnixTimePropertyName));

        return Expression.Lambda(
            Expression.Equal(deletedAtUnixTimeProperty, Expression.Constant(ActiveDeletedAtUnixTime)),
            entityParameter);
    }
}
