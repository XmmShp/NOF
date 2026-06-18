using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace NOF.Infrastructure;

internal static class SoftDeleteModelHelper
{
    public const string DeletedAtUtcPropertyName = "__DeletedAtUtc";

    public static bool ShouldConfigureSoftDelete(IMutableEntityType entityType)
        => entityType.BaseType is null
            && !entityType.IsOwned()
            && entityType.ClrType != typeof(Dictionary<string, object>);

    [RequiresDynamicCode("Calls System.Linq.Expressions.Expression.Lambda(Expression, params ParameterExpression[])")]
    [RequiresUnreferencedCode("Calls System.Linq.Expressions.Expression.Property(Expression, String)")]
    public static LambdaExpression BuildSoftDeleteFilter(Type entityClrType)
    {
        var entityParameter = Expression.Parameter(entityClrType, "entity");
        var deletedAtUtcProperty = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [typeof(DateTime?)],
            entityParameter,
            Expression.Constant(DeletedAtUtcPropertyName));

        return Expression.Lambda(
            Expression.Equal(deletedAtUtcProperty, Expression.Constant(null, typeof(DateTime?))),
            entityParameter);
    }
}
