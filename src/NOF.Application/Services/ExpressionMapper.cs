using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NOF.Application;

/// <summary>
/// Resolves, expands, validates, and executes expression-based mappings.
/// </summary>
public sealed class ExpressionMapper : IMapper
{
    private readonly Dictionary<MapKey, LambdaExpression> _expressions;
    private readonly Dictionary<MapKey, Func<IQueryable, LambdaExpression, IQueryable>> _projectors;
    private readonly ConcurrentDictionary<MapKey, Delegate> _compiledExpressions = new();

    public ExpressionMapper(MappingRegistry mappingRegistry)
    {
        ArgumentNullException.ThrowIfNull(mappingRegistry);

        var registrations = mappingRegistry.Freeze();
        var templates = new Dictionary<MapKey, LambdaExpression>();
        _projectors = new Dictionary<MapKey, Func<IQueryable, LambdaExpression, IQueryable>>();
        foreach (var registration in registrations)
        {
            ValidateRegistration(registration);
            templates[registration.Key] = registration.Expression;
            _projectors[registration.Key] = registration.Project;
        }

        _expressions = new Dictionary<MapKey, LambdaExpression>(templates.Count);
        foreach (var key in templates.Keys)
        {
            _ = Expand(key, templates, []);
        }
    }

    public Expression<Func<TSource, TDestination>> GetExpression<TSource, TDestination>(string? name = null)
    {
        var key = new MapKey(typeof(TSource), typeof(TDestination), name);
        if (!_expressions.TryGetValue(key, out var expression))
        {
            throw MissingMapping(key);
        }

        return (Expression<Func<TSource, TDestination>>)expression;
    }

    public TDestination Map<TSource, TDestination>(TSource source, string? name = null)
    {
        var key = new MapKey(typeof(TSource), typeof(TDestination), name);
        var mapping = (Func<TSource, TDestination>)_compiledExpressions.GetOrAdd(
            key,
            _ => GetExpression<TSource, TDestination>(name).Compile(preferInterpretation: true));
        return mapping(source);
    }

    public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var key = new MapKey(source.ElementType, typeof(TDestination), name);
        if (!_expressions.TryGetValue(key, out var expression)
            || !_projectors.TryGetValue(key, out var projector))
        {
            throw MissingMapping(key);
        }

        return (IQueryable<TDestination>)projector(source, expression);
    }

    private LambdaExpression Expand(
        MapKey key,
        IReadOnlyDictionary<MapKey, LambdaExpression> templates,
        IReadOnlyList<MapKey> path)
    {
        if (_expressions.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var cycleStart = IndexOf(path, key);
        if (cycleStart >= 0)
        {
            var cycle = path.Skip(cycleStart).Append(key).Select(FormatKey);
            throw new InvalidOperationException($"Circular mapping dependency detected: {string.Join(" -> ", cycle)}.");
        }

        if (!templates.TryGetValue(key, out var template))
        {
            throw MissingMapping(key);
        }

        var nextPath = path.Append(key).ToArray();
        var body = new MappingReferenceExpander((nestedKey, source) =>
        {
            var nested = Expand(nestedKey, templates, nextPath);
            return new ParameterReplacementVisitor(nested.Parameters[0], source)
                .Visit(nested.Body)!;
        }).Visit(template.Body)!;

        var expanded = Expression.Lambda(template.Type, body, template.Parameters);
        MappingExpressionValidator.Validate(expanded, key);
        _expressions[key] = expanded;
        return expanded;
    }

    private static void ValidateRegistration(MappingRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(registration.Key);
        ArgumentNullException.ThrowIfNull(registration.Expression);

        if (registration.Expression.Parameters.Count != 1
            || registration.Expression.Parameters[0].Type != registration.Key.Source
            || registration.Expression.ReturnType != registration.Key.Destination)
        {
            throw new InvalidOperationException(
                $"Mapping expression for {FormatKey(registration.Key)} must have signature " +
                $"'{registration.Key.Source.FullName} -> {registration.Key.Destination.FullName}'.");
        }
    }

    private static int IndexOf(IReadOnlyList<MapKey> path, MapKey key)
    {
        for (var index = 0; index < path.Count; index++)
        {
            if (path[index] == key)
            {
                return index;
            }
        }

        return -1;
    }

    private static InvalidOperationException MissingMapping(MapKey key)
        => new($"No mapping expression is registered for {FormatKey(key)}.");

    private static string FormatKey(MapKey key)
        => $"{key.Source.FullName} -> {key.Destination.FullName}" +
            (key.Name is null ? string.Empty : $" ('{key.Name}')");

    private sealed class MappingReferenceExpander(
        Func<MapKey, Expression, Expression> expand) : ExpressionVisitor
    {
        private static readonly MethodInfo _mappingReferenceMethod = typeof(MappingReference)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(MappingReference.Map) && method.IsGenericMethodDefinition);

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (!node.Method.IsGenericMethod
                || node.Method.GetGenericMethodDefinition() != _mappingReferenceMethod)
            {
                return base.VisitMethodCall(node);
            }

            var arguments = node.Method.GetGenericArguments();
            var name = node.Arguments.Count == 2
                ? GetMappingName(node.Arguments[1])
                : null;
            var source = Visit(node.Arguments[0]);
            return expand(new MapKey(arguments[0], arguments[1], name), source);
        }

        private static string? GetMappingName(Expression expression)
        {
            if (expression is ConstantExpression { Value: null })
            {
                return null;
            }

            if (expression is ConstantExpression { Value: string name })
            {
                return name;
            }

            throw new InvalidOperationException("Nested mapping names must be constant strings.");
        }
    }

    private sealed class ParameterReplacementVisitor(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == parameter ? replacement : base.VisitParameter(node);
    }

    private sealed class MappingExpressionValidator : ExpressionVisitor
    {
        private readonly MapKey _key;

        private MappingExpressionValidator(MapKey key)
        {
            _key = key;
        }

        public static void Validate(LambdaExpression expression, MapKey key)
            => new MappingExpressionValidator(key).Visit(expression);

        protected override Expression VisitInvocation(InvocationExpression node)
            => throw Invalid("Expression.Invoke is not allowed", node);

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(MappingReference))
            {
                throw Invalid("an unexpanded nested mapping reference remains", node);
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is not null
                && !node.Type.IsPrimitive
                && !node.Type.IsEnum
                && node.Type != typeof(string)
                && node.Type != typeof(decimal)
                && node.Type != typeof(DateTime)
                && node.Type != typeof(DateTimeOffset)
                && node.Type != typeof(TimeSpan)
                && node.Type != typeof(Guid)
                && node.Type != typeof(Type))
            {
                throw Invalid($"captured constant of type '{node.Type.FullName}' is not allowed", node);
            }

            return base.VisitConstant(node);
        }

        private InvalidOperationException Invalid(string reason, Expression node)
            => new($"Mapping expression for {FormatKey(_key)} is invalid: {reason}. Node: {node}.");
    }
}
