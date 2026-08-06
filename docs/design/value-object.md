# The NOF Value Object: Why There Is No `.Value`

## The Shape of a Value Object

A value object in NOF is a `readonly partial struct` that implements `IValueObject<T>`:

```csharp
public readonly partial struct OrderName : IValueObject<string>
{
    public static void Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainValidationException("Order name cannot be empty.");
    }
}
```

The source generator produces everything else: a private constructor, a `static Of(T)` factory that calls `Validate`, an explicit cast to `T`, equality members, `ToString`, and a nested `JsonConverter`. The developer writes only the type declaration and optional validation. That's it.
If you override `Normalize(T)`, keep it limited to canonicalization such as trimming or casing, and avoid calling `Of(...)` or `Validate(...)` from inside `Normalize`.

## String Length Is Declared Once

String-backed value objects can declare their accepted `string.Length` range on the value-object type:

```csharp
[ValueObjectLength(100, MinimumLength = 1)]
public readonly partial struct OrderName : IValueObject<string>
{
    public static void Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Order name cannot be empty.");
        }
    }
}
```

`ValueObjectGenerator` inserts the minimum- and maximum-length checks after `Normalize` and before the custom `Validate` method. `MinimumLength` defaults to `0`. EF Core and NHibernate read `MaximumLength` when constructing their models, so entity mappings do not repeat `HasMaxLength(100)`; minimum length remains a domain validation rule rather than a database column facet.

Explicit constant persistence configuration is diagnosed at compilation time:

- `NOF306` warns when `HasMaxLength` repeats the value object's declared length.
- `NOF307` is an error when `HasMaxLength` conflicts with the value object's declared length.
- `NOF308` warns when infrastructure declares a constant `HasMaxLength` but the value object has no `ValueObjectLength` declaration.

The analyzer deliberately ignores values that Roslyn cannot evaluate as constants. Persistence model construction still rejects a dynamically configured conflicting length at runtime. Code fixes remove redundant/conflicting infrastructure calls, or move a missing constant declaration onto a source value object and remove the infrastructure call. `NOF308` supports Fix All for a document, project, or solution; repeated mappings of the same value object are merged into one attribute declaration. If those mappings specify different lengths, that value object is left unchanged for manual resolution.

`ValueObjectLength` uses `string.Length`; persistence providers translate that declaration into their maximum-length facet. Provider-specific byte limits, index-size limits, and explicit column types remain infrastructure concerns.

One thing the developer does _not_ get is a `.Value` property. This is deliberate, and the rest of this document explains why.

## The Temptation of `.Value`

The obvious API for extracting the underlying primitive from a value object is a property:

```csharp
long id = orderId.Value;       // Hypothetical
string name = orderName.Value; // Hypothetical
```

This is what many value object libraries do. It is readable, discoverable, and familiar. So why didn't we do it?

## Reason 1: Independence of the Value Object

A value object is supposed to _be_ its value, not _contain_ its value. An `OrderName` is a `string` with extra semantics — it's not a box around a `string`. The `.Value` property turns the value object into a container, subtly shifting the mental model from "this _is_ a name" to "this _has_ a name inside it."

The explicit cast preserves the correct mental model:

```csharp
string raw = (string)orderName;  // "Treat this OrderName as a string"
```

This reads as a type conversion, not as reaching inside a wrapper to pull something out. The distinction matters when the codebase has hundreds of value objects — you want developers to think of them as types in their own right, not as wrappers to be peeled off.

## Reason 2: Expression Tree Translation (EF Core, LINQ)

This is the pragmatic reason, and it's the one that settled the debate.

Consider an EF Core query:

```csharp
var order = await dbContext.Orders
    .Where(o => o.Name == orderName)
    .FirstOrDefaultAsync();
```

For this to work, EF Core needs to translate the `==` comparison into SQL. If `OrderName` is stored in the database as a `string` column (via a `ValueConverter`), then the expression tree must resolve to a comparison between the column value and the primitive.

With a `.Value` property, the query would need to be:

```csharp
.Where(o => o.Name.Value == orderName.Value)  // Both sides unwrapped
```

This is not just ugly — it's fragile. EF Core's expression tree translator needs to understand that `.Value` on a value object means "access the underlying column." Some EF Core value converter configurations handle this; many don't, leading to runtime `InvalidOperationException` or client-side evaluation.

With explicit casts and the `ValueConverter` properly configured, EF Core sees the value object type directly in the expression tree and knows how to translate it:

```csharp
.Where(o => o.Name == orderName)  // Just works — EF Core uses the ValueConverter
```

No `.Value`, no expression tree gymnastics. The value object participates in the query as a first-class type, and the `ValueConverter` handles the SQL translation transparently.

This extends to equality, grouping, projections, and other LINQ operations. Ordering is the exception: LINQ's default comparer cannot order a generated value object because NOF deliberately generates equality semantics but not domain-specific ordering semantics.

When an `OrderBy` key is a value object, order by its primitive representation explicitly:

```csharp
var orders = query.OrderBy(order => (long)order.Id);
```

Diagnostic `NOF015` reports `OrderBy`, `OrderByDescending`, `ThenBy`, and `ThenByDescending` keys that are value objects. The diagnostic is suppressed when the value object explicitly implements `IComparable<TSelf>` or `IComparable`, because that declaration makes the domain's ordering semantics intentional.

## Reason 3: Discouraging Unwrapping

If extracting the primitive is easy, developers do it reflexively. A `.Value` property invites code like:

```csharp
logger.LogInformation("Processing order {Name}", orderName.Value);
SendEmail(customer.Email.Value);
```

But `OrderName` already has `ToString()`. And the email-sending method should probably accept `EmailAddress`, not `string`. Every call to `.Value` is a place where the type safety of the value object is discarded. Making unwrapping slightly inconvenient — requiring an explicit cast — creates just enough friction to make developers pause and ask: "Do I really need the primitive here, or should I pass the value object?"

The explicit cast is an intentional speed bump:

```csharp
string raw = (string)orderName;  // You have to think about this
```

## Extraction Model

There is no separate `.Value` property or framework-provided `GetUnderlyingValue()` API on `IValueObject<T>`.

When you need the primitive, use the explicit cast generated for the value object:

```csharp
string raw = (string)orderName;
```

That keeps the public API small and consistent with how the framework's mapper and EF Core integration already reason about value objects.

## How This Interacts with the Mapper

The `[Mappable]` source generator understands value objects natively:

- **Unwrap**: `IValueObject<T>` → `T` via explicit cast `(T)value`. Only the exact underlying type is supported — a `string` value object won't auto-convert to `int`.
- **Wrap**: `T` → `VoType.Of(value)`. Same exact-type restriction.
- **Cross-VO**: `OrderName` → `CustomerId` always falls back to `IMapper`, because cross-VO mapping is a domain decision that should be explicit.

The generator never looks for `.Value`. It uses the same explicit cast that hand-written code would use. The generated code is what you would have written — there is no special path for value objects that diverges from the language's own conversion mechanics.

## Summary

| Approach                          | Mental Model         | EF Core LINQ                  | Unwrap Friction      | NOF Choice |
| --------------------------------- | -------------------- | ----------------------------- | -------------------- | ---------- |
| `.Value` property                 | Container            | Problematic (expression tree) | Low (too easy)       | ❌         |
| Explicit cast `(T)`               | Type conversion      | Transparent (ValueConverter)  | Medium (intentional) | ✅         |
| No extra API beyond explicit cast | Small public surface | N/A                           | Medium (intentional) | ✅         |

The value object is a type, not a box. Treat it as one.
