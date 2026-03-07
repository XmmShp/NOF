# The NOF Value Object: Why There Is No `.Value`

## The Shape of a Value Object

A value object in NOF is a `readonly partial struct` that implements `IValueObject<T>`:

```csharp
public readonly partial struct OrderName : IValueObject<string>
{
    public static void Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException("Order name cannot be empty.");
    }
}
```

The source generator produces everything else: a private constructor, a `static Of(T)` factory that calls `Validate`, an explicit cast to `T`, equality members, `ToString`, and a nested `JsonConverter`. The developer writes only the type declaration and optional validation. That's it.

One thing the developer does *not* get is a `.Value` property. This is deliberate, and the rest of this document explains why.

## The Temptation of `.Value`

The obvious API for extracting the underlying primitive from a value object is a property:

```csharp
long id = orderId.Value;       // Hypothetical
string name = orderName.Value; // Hypothetical
```

This is what many value object libraries do. It is readable, discoverable, and familiar. So why didn't we do it?

## Reason 1: Independence of the Value Object

A value object is supposed to *be* its value, not *contain* its value. An `OrderName` is a `string` with extra semantics — it's not a box around a `string`. The `.Value` property turns the value object into a container, subtly shifting the mental model from "this *is* a name" to "this *has* a name inside it."

The explicit cast preserves the correct mental model:

```csharp
string raw = (string)orderName;  // "Treat this OrderName as a string"
```

This reads as a type conversion, not as reaching inside a wrapper to pull something out. The distinction matters when the codebase has hundreds of value objects — you want developers to think of them as types in their own right, not as wrappers to be peeled off.

## Reason 2: Expression Tree Translation (EF Core, LINQ)

This is the pragmatic reason, and it's the one that settled the debate.

Consider a repository query:

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

This extends to ordering, grouping, projections, and any other LINQ operation. Every place where you'd write `.Value`, you'd be asking the expression tree translator to do extra work — work that it might not do correctly.

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

## The Escape Hatch: `GetUnderlyingValue()`

For the rare cases where you genuinely need the primitive at a generic/infrastructure level (serialization, reflection-based tooling, diagnostics), the `IValueObject<T>` interface provides `GetUnderlyingValue()`:

```csharp
T GetUnderlyingValue();                        // IValueObject<T>
object IValueObject.GetUnderlyingValue();      // IValueObject (non-generic, boxed)
```

This method exists on the interface, not on the struct's public surface — it's `[EditorBrowsable(Never)]` at the non-generic level. IntelliSense won't suggest it. You have to know it's there. That's the right level of friction for an infrastructure-only escape hatch.

## How This Interacts with the Mapper

The `[Mappable]` source generator understands value objects natively:

- **Unwrap**: `IValueObject<T>` → `T` via explicit cast `(T)value`. Only the exact underlying type is supported — a `string` value object won't auto-convert to `int`.
- **Wrap**: `T` → `VoType.Of(value)`. Same exact-type restriction.
- **Cross-VO**: `OrderName` → `CustomerId` always falls back to `IMapper`, because cross-VO mapping is a domain decision that should be explicit.

The generator never looks for `.Value`. It uses the same explicit cast that hand-written code would use. The generated code is what you would have written — there is no special path for value objects that diverges from the language's own conversion mechanics.

## Summary

| Approach | Mental Model | EF Core LINQ | Unwrap Friction | NOF Choice |
|----------|-------------|--------------|-----------------|------------|
| `.Value` property | Container | Problematic (expression tree) | Low (too easy) | ❌ |
| Explicit cast `(T)` | Type conversion | Transparent (ValueConverter) | Medium (intentional) | ✅ |
| `GetUnderlyingValue()` | Infrastructure escape hatch | N/A | High (interface-only) | ✅ (infra only) |

The value object is a type, not a box. Treat it as one.
