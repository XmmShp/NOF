using System.Linq.Expressions;

namespace NOF.Application;

/// <summary>
/// Describes a provider-agnostic batch update specification for a queryable entity set.
/// </summary>
public interface IUpdateSetters<TSource>
{
    IReadOnlyList<UpdateSetPropertyCall<TSource>> SetPropertyCalls { get; }
}

/// <summary>
/// Fluent builder for batch update assignments.
/// </summary>
public sealed class UpdateSetters<TSource> : IUpdateSetters<TSource>
{
    private readonly List<UpdateSetPropertyCall<TSource>> _setPropertyCalls = [];

    public IReadOnlyList<UpdateSetPropertyCall<TSource>> SetPropertyCalls => _setPropertyCalls;

    public UpdateSetters<TSource> SetProperty<TProperty>(
        Expression<Func<TSource, TProperty>> propertyExpression,
        TProperty value)
    {
        ArgumentNullException.ThrowIfNull(propertyExpression);
        _setPropertyCalls.Add(new ConstantUpdateSetPropertyCall<TSource>(propertyExpression, value));
        return this;
    }

    public UpdateSetters<TSource> SetProperty<TProperty>(
        Expression<Func<TSource, TProperty>> propertyExpression,
        Expression<Func<TSource, TProperty>> valueExpression)
    {
        ArgumentNullException.ThrowIfNull(propertyExpression);
        ArgumentNullException.ThrowIfNull(valueExpression);
        _setPropertyCalls.Add(new ComputedUpdateSetPropertyCall<TSource>(propertyExpression, valueExpression));
        return this;
    }
}

/// <summary>
/// Base contract for a single property assignment inside a batch update.
/// </summary>
public abstract record UpdateSetPropertyCall<TSource>(LambdaExpression PropertyExpression)
    ;

/// <summary>
/// Represents assigning a constant value to a property in a batch update.
/// </summary>
public sealed record ConstantUpdateSetPropertyCall<TSource>(
    LambdaExpression PropertyExpression,
    object? Value) : UpdateSetPropertyCall<TSource>(PropertyExpression);

/// <summary>
/// Represents assigning a computed value expression to a property in a batch update.
/// </summary>
public sealed record ComputedUpdateSetPropertyCall<TSource>(
    LambdaExpression PropertyExpression,
    LambdaExpression ValueExpression) : UpdateSetPropertyCall<TSource>(PropertyExpression);
