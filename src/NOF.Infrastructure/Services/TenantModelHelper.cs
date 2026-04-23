using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace NOF.Infrastructure;

internal static class TenantModelHelper
{
    public const string TenantIdPropertyName = "TenantId";
    public const int TenantIdMaxLength = 256;
    public const string TenantScopedAnnotationName = "NOF:TenantScoped";
    public const string HostOnlyAnnotationName = "NOF:HostOnly";

    public static HashSet<Type> CreateHostOnlyTypeSet(NOFDbContext dbContext)
        => [.. dbContext.GetHostOnlyEntityTypes()];

    public static bool ShouldConfigureTenantBehavior(IMutableEntityType entityType)
        => entityType.BaseType is null
            && !entityType.IsOwned()
            && entityType.ClrType != typeof(Dictionary<string, object>);

    public static bool IsHostOnlyType(Type clrType, HashSet<Type> hostOnlyTypes)
        => clrType.IsDefined(typeof(HostOnlyAttribute), inherit: true)
            || hostOnlyTypes.Contains(clrType);

    public static bool IsTenantScopedEntity(IReadOnlyEntityType entityType)
        => entityType.FindAnnotation(TenantScopedAnnotationName)?.Value as bool? == true;

    [RequiresDynamicCode("Calls System.Linq.Expressions.Expression.Lambda(Expression, params ParameterExpression[])")]
    [RequiresUnreferencedCode("Calls System.Linq.Expressions.Expression.Property(Expression, String)")]
    public static LambdaExpression BuildTenantFilter(Type entityClrType, NOFDbContext dbContext)
    {
        var entityParameter = Expression.Parameter(entityClrType, "entity");
        var tenantProperty = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [typeof(string)],
            entityParameter,
            Expression.Constant(TenantIdPropertyName));
        var currentTenant = Expression.Property(
            Expression.Constant(dbContext),
            nameof(NOFDbContext.CurrentTenantId));

        return Expression.Lambda(
            Expression.Equal(tenantProperty, currentTenant),
            entityParameter);
    }

}
