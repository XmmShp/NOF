# Expression-Based Mapping

## Design

NOF has one mapping model: a registered expression is the source of truth for both query projection and in-memory mapping.

- `MappingRegistration` stores one `LambdaExpression` for a closed `(source, destination, name)` key and a generic query applicator created by `Of<TSource, TDestination>()`.
- `MappingRegistry` collects registrations while application parts are initialized and freezes when `IMapper` is created.
- `ExpressionMapper.GetExpression(...)` returns the fully expanded expression for `IQueryable.Select`.
- `ExpressionMapper.Map(...)` compiles and caches that same expression with interpretation enabled for AOT compatibility.
- `Mapper.Current` exposes the current scope's mapper through an async-flow-local binding.
- `ProjectTo<TDestination>()` applies the ambient expression without introducing an EF Core dependency into `NOF.Application`.

There is no runtime-type object mapping, open-generic fallback, or delegate-only mapping path. The untyped query projection path exists only to recover the source key from `IQueryable.ElementType`; it still applies the same registered expression through an AOT-safe generic applicator.

## Core Types

```csharp
public sealed record MapKey(Type Source, Type Destination, string? Name = null);

public sealed record MappingRegistration
{
    public MapKey Key { get; }
    public LambdaExpression Expression { get; }

    public static MappingRegistration Of<TSource, TDestination>(
        Expression<Func<TSource, TDestination>> expression,
        string? name = null);
}

public interface IMapper
{
    Expression<Func<TSource, TDestination>>
        GetExpression<TSource, TDestination>(string? name = null);

    IQueryable<TDestination> ProjectTo<TDestination>(
        IQueryable source,
        string? name = null);

    TDestination Map<TSource, TDestination>(
        TSource source,
        string? name = null);
}
```

## Source-Generated Registration

Declare every mapping direction explicitly:

```csharp
[Mappable<Order, OrderDto>]
[Mappable<Order, OrderSummary>]
public static partial class Mappings;
```

The source generator emits an assembly initializer that adds `MappingRegistration` expressions to `MappingRegistry`. A mapping must be representable as an expression. The generator never falls back to a runtime `IMapper.Map` call.

Generated conversions support:

- property and constructor matching by name, case-insensitively
- direct and built-in numeric conversions
- nullable value conversions
- `IValueObject<T>` wrapping and unwrapping
- collection projection through `Select`, `ToList`, and `ToArray`
- nested registered mappings

Provider-sensitive conversions such as `Parse`, `Enum.Parse`, and arbitrary application methods are not generated automatically.

Register a deliberate custom expression during service composition when automatic generation is inappropriate:

```csharp
services.AddMapping<ExternalOrder, OrderDto>(
    order => new OrderDto(order.Id, order.DisplayName));
```

## Query Projection

Filtering, ordering, and paging belong before the final projection:

```csharp
var orders = await dbContext.Set<Order>()
    .AsNoTracking()
    .Where(order => order.Status == OrderStatus.Active)
    .OrderBy(order => order.Id)
    .ProjectTo<OrderDto>()
    .ToListAsync(cancellationToken);
```

The extension receiver is `IQueryable`, so only the destination generic argument is required and the source key comes from `ElementType`. Each registration carries a statically constructed generic query applicator, avoiding runtime `MakeGenericMethod` and keeping the API NativeAOT-safe. `ProjectTo` does not call `Compile()`, insert `AsEnumerable()`, or capture the mapper in the query tree.

NOF binds `Mapper.Current` when the dependency injection scope's daemon services are resolved. The binding uses `AsyncLocal`, supports nested scopes, and is restored when the scope is disposed. Standalone code and tests can use the explicit overload:

```csharp
var projected = query.ProjectTo<OrderDto>(mapper);
```

Diagnostic `NOF025` warns when server-side query shaping is applied after `ProjectTo`, including filtering, ordering, paging, set operations, another projection, and predicate-bearing terminal operations such as `FirstOrDefault(predicate)` or `AnyAsync(predicate)`. It follows direct chains plus single-initializer local variables and aliases.

To limit false positives, the analyzer does not warn for plain materialization or execution, client-side work after `AsEnumerable`, query tags, returning the projected query, passing it to a non-query consumer, or locals whose origin becomes ambiguous through reassignment or conditional flow. These conservative cases can produce false negatives; the rule is advisory and intentionally prefers a missed warning over claiming an uncertain projection order.

## Nested Mapping

Generated templates use `MappingReference.Map<TSource, TDestination>(...)` to mark a nested mapping. `ExpressionMapper` recursively replaces every marker with the nested expression body before exposing the expression.

Expansion:

- substitutes parameters directly; it never emits `Expression.Invoke`
- resolves named mappings exactly
- detects missing registrations and dependency cycles
- rejects captured service constants
- validates every mapping eagerly when `ExpressionMapper` is created

## Diagnostics

- `NOF020`: duplicate mapping declaration
- `NOF021`: `[Mappable]` target must be `partial static`
- `NOF022`: incompatible optional or nullable semantics
- `NOF023`: required nested mapping is not declared
- `NOF024`: destination constructor parameter or required member cannot be bound
- `NOF025`: server-side query shaping continues after `ProjectTo`

Provider translation support is verified with relational integration tests rather than EF Core's in-memory provider.
