# The NOF Mapper: A Story of Deliberate Simplicity

## Why We Built a Mapper at All

Every framework that deals with domain-driven design eventually faces the same question: how do you move data between your domain entities and the DTOs that cross process boundaries? The answer, in most ecosystems, is a mapper — some piece of infrastructure that knows how to turn an `Order` into an `OrderDto`.

We could have reached for AutoMapper, Mapster, or any number of libraries. But NOF is an opinionated framework, and one of its core opinions is that **explicit is better than implicit**. A library that scans your types at startup, matches properties by name, and silently fills in the blanks felt like the opposite of what we wanted. So we wrote our own.

The original goal was modest: give developers a place to register a function that converts `A` to `B`, keyed by their types, and call it when needed. No reflection. No property-name matching. Just a dictionary of functions.

That was the right instinct. What happened next was not.

## How We Lost Our Way

Once the basic mapper was working, convenience started whispering. "Wouldn't it be nice if `int` could automatically map to `string` via `ToString()`?" Sure. "What about `IValueObject<T>` → `T`? That's so common it should just work." Of course. "And `Optional<T>` → `T`? `Result<T>` → `T`? Enum ↔ numeric?" Why not.

Before long, we had a `MapKey.Any` wildcard that held a list of built-in fallback rules. We introduced an `IUnwrappable` interface so that `Optional<T>` and `Result<T>` could expose their inner values without reflection. Every mapping delegate returned a three-state `MapResult`:

- `None` — "I don't handle this."
- `Final` — "Here's your answer."
- `Continue` — "Here's an intermediate value; keep going."

The `Continue` state was the clever part. It turned the mapper into a recursive engine. `Optional<Optional<int>>` → `int`? No problem — unwrap once, get `Optional<int>`, unwrap again, get `int`. `OrderId` (a value object wrapping `long`) → `string`? Unwrap to `long`, convert to `string`. Chains of arbitrary depth, all automatic, controlled by a `MaxRecursionDepth` setting.

Each `MapKey` could hold multiple delegates in a `List<MapFunc>`, evaluated in last-in-first-out order. There was `Add`, `TryAdd`, and `ReplaceOrAdd`. The `MapFunc` delegate took four parameters: `(Type sourceType, Type destType, object source, IMapper mapper)`.

It worked. All 82 tests passed. And it was too complicated.

## The Moment of Clarity

The turning point came during a design review. We looked at the mapper and asked a simple question: *who is this for?*

The built-in rules were supposed to save developers from writing trivial mappings. But domain-to-DTO mapping is not trivial — it is *the* place where you define the shape of your API contract. Making it invisible doesn't save time; it hides intent. When a junior developer sees `mapper.Map<OrderId, string>(orderId)` succeed without any registration, they don't think "how convenient." They think "where is this defined?" And the answer — "it's a built-in rule that unwraps the value object via `IUnwrappable`, then chains to a `ToString` conversion through the recursive `Continue` mechanism" — is not a satisfying one.

The recursive mapping was elegant in theory but treacherous in practice. A `Continue` result could trigger an unbounded chain of intermediate transformations. Debugging required understanding a state machine. The `MaxRecursionDepth` guard existed only because infinite loops were a real possibility.

Multiple delegates per key solved a problem nobody had. In every real usage we examined, each type pair had exactly one mapping function. The multi-delegate design added cognitive overhead (which delegate wins?) for zero practical benefit.

`ReplaceOrAdd` was redundant once we accepted that `Add` should simply replace.

We had built a sophisticated engine to solve problems that didn't need solving. The original instinct — a dictionary of functions — was right all along.

## The Design We Came Back To

The simplified mapper is, at its core, exactly what we set out to build in the first place: a `ConcurrentDictionary<MapKey, MapFunc>`.

### One key, one function

A `MapKey` is a triple of `(Source type, Destination type, optional Name)`. Each key maps to exactly one delegate. If you call `Add` twice with the same key, the second registration replaces the first. If you call `TryAdd`, it only registers if the key is vacant. That's the entire registration model.

```csharp
// This is all you need
o.Add<Order, OrderDto>(order => new OrderDto(order.Id, order.Total));
```

### The delegate is honest

`MapFunc` takes an `object` and an `IMapper`, and returns an `object`. No three-state return type. No signal for "keep going." The function you register is the function that runs, and whatever it returns is the result. If you need to map a nested object inside your delegate, you have the `IMapper` right there — call it explicitly.

```csharp
o.Add<Order, OrderSummary>((order, mapper) =>
    new OrderSummary(
        order.Id,
        mapper.Map<Address, AddressDto>(order.ShippingAddress)));
```

This is more characters than having the engine recursively chase intermediate values. It is also *immediately obvious* what is happening. A developer reading this code for the first time knows exactly which mappings are being invoked and in what order. There is no hidden state machine. There is no recursion depth to worry about. There is no `Continue`.

### TryMap speaks C\#

When we had `MapResult`, our `TryMap` returned an `Optional<TDestination>` — a framework-specific wrapper. But C# already has a well-known pattern for "try to do something, tell me if it worked": `bool TryXxx(input, out result)`. Every .NET developer recognizes it from `int.TryParse`, `Dictionary.TryGetValue`, and dozens of other APIs. So that's what `TryMap` does now.

```csharp
if (mapper.TryMap<Order, OrderDto>(order, out var dto))
{
    // use dto
}
```

No framework-specific wrapper types. No guessing what `HasValue` means in this context. Just a boolean and an `out` parameter.

### The one exception: Nullable fallback

We kept exactly one piece of implicit behavior. If you register `A → T`, and someone asks for `A → T?`, the mapper will use the `A → T` mapping and box the result into a nullable. This works because it is universally intuitive — if you know how to produce a `T`, you obviously know how to produce a `T?` — and because the alternative (forcing users to register both `A → T` and `A → T?`) would be pure ceremony.

The reverse does *not* apply: having `A → T?` does not satisfy a request for `A → T`. Nullability only widens, never narrows.

### What we deleted

Removing features is harder than adding them, because you have to be sure nobody needs what you're taking away. Here's what went:

- **`MapResult` and `MapResultKind`** — the three-state return type that powered recursion. Gone.
- **`IUnwrappable`** — the interface that `Optional<T>` and `Result<T>` implemented so the mapper could extract their inner values at runtime. Removed from both types.
- **`MapKey.Any`** — the wildcard key that held built-in fallback rules. Deleted.
- **All built-in mappings** — `ToString`, numeric conversions, enum conversions, value object unwrapping, `Optional<T>` unwrapping, `Result<T>` unwrapping. All gone.
- **`MaxRecursionDepth`** — no recursion means no depth limit.
- **`ReplaceOrAdd`** — `Add` already replaces.
- **Multiple delegates per key** — a `List<MapFunc>` became a single `MapFunc`.

The test count dropped from 82 to 35. Not because we test less, but because there is less to test.

## Lookup Resolution

When you call `Map<A, B>()`, the mapper looks for a delegate in this order:

1. **Exact match** — `MapKey(typeof(A), typeof(B), name)`
2. **Open generic source** — `MapKey(typeof(A<>), typeof(B), name)` (if `A` is a closed generic)
3. **Open generic destination** — `MapKey(typeof(A), typeof(B<>), name)` (if `B` is a closed generic)
4. **Both open** — `MapKey(typeof(A<>), typeof(B<>), name)`
5. **Nullable fallback** — if `B` is `Nullable<T>`, retry with `T` as the destination

This covers the realistic cases — mapping from/to generic collections, mapping to nullable value types — without any wildcard catch-all.

## The Type Signatures

For the record, here is every public type in the mapper:

```csharp
// The delegate: source in, result out, mapper available for nesting
public delegate object MapFunc(object source, IMapper mapper);

// The key: what are we mapping, and under what name?
public sealed record MapKey(Type Source, Type Destination, string? Name = null);

// The options: just a dictionary with convenience methods
public sealed class MapperOptions : ConcurrentDictionary<MapKey, MapFunc>
{
    MapperOptions Add<TSource, TDest>(Func<TSource, TDest> func, string? name = null);
    MapperOptions Add<TSource, TDest>(Func<TSource, IMapper, TDest> func, string? name = null);
    bool TryAdd<TSource, TDest>(Func<TSource, TDest> func, string? name = null);
    // ... non-generic overloads, Merge
}

// The interface: map or try to map
public interface IMapper
{
    TDest Map<TSource, TDest>(TSource source, bool useRuntimeType = false, string? name = null);
    bool TryMap<TSource, TDest>(TSource source, out TDest result, bool useRuntimeType = false, string? name = null);
    // ... non-generic overloads, Add, TryAdd
}
```

That's it. No `MapResult`. No `IUnwrappable`. No recursion engine. A dictionary of functions, a lookup algorithm, and a nullable fallback. The rest is up to you — explicitly.

## The Source Generator: Meeting in the Middle

After shipping the simplified mapper, we heard a recurring piece of feedback: "I love that mappings are explicit, but writing `new OrderDto(order.Id, order.Name, order.Status.ToString())` for twenty properties is tedious."

They were right. The philosophy was sound — explicit over implicit — but the ergonomics needed help. The question was how to reduce boilerplate without reintroducing hidden magic.

The answer was a source generator. Instead of the mapper engine discovering rules at runtime, we let the compiler generate the mapping functions at build time. The mapping code is visible, inspectable, and deterministic. If you want to know what a mapping does, you can read the generated file. There is no runtime reflection, no scanning, no convention engine.

### How it works

You declare your mapping pairs on a `partial static class` using `[Mappable<TSource, TDest>]`:

```csharp
[Mappable<Order, OrderDto>]
[Mappable<Order, OrderSummary>(TwoWay = true)]
public static partial class Mappings;
```

The generator produces a `ConfigureAutoMappings()` extension method on `MapperOptions`:

```csharp
// Generated
partial class Mappings
{
    public static MapperOptions ConfigureAutoMappings(this MapperOptions options)
    {
        options.Add<Order, OrderDto>(src =>
            new OrderDto(src.Id, src.Name)
            {
                Id = src.Id,
                Name = src.Name,
            });
        // ... reverse mapping for TwoWay, etc.
        return options;
    }
}
```

You call it at startup: `services.Configure<MapperOptions>(o => o.ConfigureAutoMappings())`.

Attributes can be scattered across multiple partial declarations of the same class — the generator merges them. This lets you co-locate mapping declarations near the types they relate to, without losing the single registration point.

### The matching rules

The generator follows a small, predictable set of rules:

1. **Property matching** — only public, same-name (case-insensitive) properties. If the source has `Id` and the destination has `Id`, they match. If the names don't match, they're ignored.

2. **Constructor selection** — the generator examines all public constructors and picks the one whose parameter names match the most source properties (case-insensitive, matching the C# convention where `record OrderDto(int id)` has a parameter `id` that corresponds to property `Id`). Even if a property is passed to the constructor, it still appears in the member initializer if it's writable — this is intentional and matches how records work.

3. **Implicit conversions (including user-defined)** — if C#'s `ClassifyConversion` reports an implicit conversion between the source and destination types, the generator emits a direct assignment. This naturally handles `T → Optional<T>`, `T → Result<T>`, and other user-defined implicit operators without special-casing. The generator follows C#'s own implicit conversion rules.

4. **User-defined explicit conversions** — if no implicit conversion exists but the source or destination type declares an explicit conversion operator, the generator emits a cast. This handles `IValueObject<T>` → `T` unwrapping via the generated `explicit operator`.

5. **Optional / Result unwrap** — strict nullable semantics on the unwrap side:

   | Source | Destination | Allowed? | Reason |
   |--------|-------------|----------|--------|
   | `Wrapper<T>` | `T` | ❌ NOF022 | Unwrapping discards "absent" semantics; destination must be nullable |
   | `Wrapper<T>` | `T?` | ✅ | Safe unwrap |
   | `Wrapper<T?>` | `T` | ❌ NOF022 | Double-nullable source cannot narrow |
   | `Wrapper<T?>` | `T?` | ❌ NOF022 | Inner nullable makes absence ambiguous |

   - `Optional<T>` unwraps via `.Value`, `Result<T>` unwraps via `.Value!`.
   - Wrapping (`T → Wrapper<T>`) is handled by rule 3 (implicit conversion) — no special logic needed.

6. **IValueObject\<T\>** (`T : notnull`):
   - Unwrap: `IValueObject<T>` → `T` via explicit cast (rule 4). Only the exact underlying type is allowed.
   - Wrap: `T` → `VoType.Of(value)`. Same exact-type restriction.
   - Different VO types (e.g. `OrderName` → `CustomerId`) always fall back to `IMapper`.

7. **Nullable\<T\> (value types)** — `Nullable<VO>` → `T?` and `T?` → `Nullable<VO>` are expanded via `.HasValue` / `.Value` with recursive inner conversion. This handles common patterns like `ConfigNodeId?` → `long?`.

8. **IEnumerable\<T\> → collection** — if both source and destination implement `IEnumerable<T>` (including `List<T>`, `IReadOnlyList<T>`, arrays, custom collections), the generator emits a collection expression `[..src.Select(item => convert(item))]`. The compiler infers the correct target collection type from context. Element conversion is recursive — all the above rules apply per-element.

9. **Common primitive conversions** — `int` ↔ `string` (via `ToString()` / `Parse`), `int` ↔ `enum` (via cast), `enum` ↔ `string` (via `ToString()` / `Enum.Parse<T>`), numeric casts between numeric types.

10. **Everything else uses IMapper** — if none of the above rules apply, the generated code calls `mapper.Map<TSource, TDest>(value)`. If that pair is not among the auto-generated registrations, the generator emits a **NOF023** warning at compile time. This means you can compose auto-generated mappings with manually registered ones seamlessly, but you get early feedback when a mapping might be missing.

### Why this isn't magic

The key difference between this source generator and a library like AutoMapper is transparency. AutoMapper resolves mappings at runtime through a configuration object that you can't easily inspect at a glance. Our generator produces C# code that you can read, step through in a debugger, and reason about statically. The generated code is exactly what you would have written by hand — it just saves you the keystrokes.

If the generator can't figure out a conversion, it doesn't silently skip the property or throw at runtime. It emits a call to `mapper.Map<,>()`, which will fail loudly at runtime if you haven't registered that mapping. The failure mode is the same as hand-written code: explicit and immediate. And you'll see a compile-time warning (NOF023) alerting you to the gap.

### Diagnostics

- **NOF020** — duplicate mapping. If the same `(Source, Destination)` pair appears twice — including reverse mappings from `TwoWay = true` — you get a compile error. Not a runtime exception, not a silent last-wins override. A compile error.
- **NOF021** — the `[Mappable]` class must be `partial static`. If it isn't, you get a compile error telling you to fix it.
- **NOF022** — nullable semantic mismatch. The generator detected a wrapper unwrap that violates nullable annotation rules (e.g. `Optional<T>` → `T` without `T?`). The property falls back to `mapper.Map` with this warning.
- **NOF023** — unregistered mapper fallback. The generated code calls `mapper.Map<S, D>()` for a type pair that is not among the auto-generated registrations. This is a hint that you may need to register a manual mapping or add a `[Mappable]` declaration.
