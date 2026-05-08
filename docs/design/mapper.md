# Mapper

## Current Model

NOF's mapper is intentionally explicit.

- `IMapper` performs runtime mapping.
- `ManualMapper` is the default implementation registered by `NOF.Infrastructure`.
- `MapperRegistration` stores one mapping delegate for one `(source, destination, name)` key.
- `MapperInfos` materializes source-generated and manual registrations from `Registry.MapperRegistrations` and freezes them on first read.
- `MapperInitializationStep` assigns the resolved mapper instance to `Mapper.Current` after the host is built.

## Core Types

```csharp
public delegate object MapFunc(object source, IMapper mapper);

public sealed record MapKey(Type Source, Type Destination, string? Name = null);

public sealed record MapperRegistration(MapKey Key, MapFunc MappingFunc)
{
    public static MapperRegistration Of<TSource, TDestination>(Func<TSource, TDestination> mappingFunc, string? name = null);
    public static MapperRegistration Of<TSource, TDestination>(Func<TSource, IMapper, TDestination> mappingFunc, string? name = null);
}

public interface IMapper
{
    TDestination Map<TSource, TDestination>(TSource source, bool useRuntimeType = false, string? name = null);
    bool TryMap<TSource, TDestination>(TSource source, out TDestination result, bool useRuntimeType = false, string? name = null);
    object Map(Type sourceType, Type destinationType, object source, string? name = null);
    bool TryMap(Type sourceType, Type destinationType, object source, out object? result, string? name = null);
}
```

## Source-Generated Registration

You declare mapping pairs on a `partial static class` using `[Mappable]`:

```csharp
[Mappable<Order, OrderDto>]
[Mappable<Order, OrderSummary>(TwoWay = true)]
public static partial class Mappings;
```

The source generator emits an assembly initializer that adds `MapperRegistration` entries into `Registry.MapperRegistrations`.
Those registrations become active when the assembly is loaded through `AddApplicationPart(...)`.
No extra mapper bootstrap code is required in `Program.cs`.

## Runtime Resolution Order

`ManualMapper` resolves mappings in this order:

1. exact source and destination type pair
2. open generic source
3. open generic destination
4. open generic source plus open generic destination
5. nullable destination fallback (`A -> T?` can reuse `A -> T`)

The fallback only widens the destination. A mapping registered as `A -> T?` does not satisfy `A -> T`.

## Generated Matching Rules

The `[Mappable]` source generator follows a small, explicit rule set:

- match public properties by name, case-insensitively
- choose the public constructor with the most matched parameters
- use implicit conversions when C# already supports them
- use explicit conversion operators when available
- unwrap `Optional<T>` and `Result<T>` only when nullable semantics are safe
- support `IValueObject<T>` wrapping and unwrapping
- support `Nullable<T>` and `IEnumerable<T>` element conversion recursively
- fall back to `IMapper` when no direct codegen rule applies

## Diagnostics

Current mapping diagnostics include:

- `NOF020`: duplicate mapping registration
- `NOF021`: `[Mappable]` target must be `partial static`
- `NOF022`: nullable semantic mismatch during wrapper unwrap
- `NOF023`: generated code falls back to an `IMapper` mapping that is not auto-generated
