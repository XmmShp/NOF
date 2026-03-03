# NOF.Domain

Domain layer package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the foundational building blocks for domain-driven design: entities, aggregate roots, repositories, domain events, and domain-specific annotations with source generation support.

## Key Abstractions

### Entities & Aggregate Roots

```csharp
public class Order : AggregateRoot
{
    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }

    public void Confirm()
    {
        Status = OrderStatus.Confirmed;
        AddEvent(new OrderConfirmedEvent(Id));
    }
}
```

### Repository

```csharp
public interface IRepository<TAggregateRoot> where TAggregateRoot : class, IAggregateRoot
{
    ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken cancellationToken = default);
    void Add(TAggregateRoot entity);
    void Remove(TAggregateRoot entity);
}
```

### `[Failure]` Attribute

Declaratively define domain failure codes. The source generator produces static `FailResult` instances at compile time.

```csharp
[Failure("NotFound", "Order not found", 404001)]
[Failure("AlreadyPaid", "Order has already been paid", 409001)]
public partial class OrderFailures;

// Generated usage:
return OrderFailures.NotFound;
```

### `IValueObject<T>` Interface

Implement `IValueObject<T>` on a `readonly partial struct` to define a value object. The source generator produces:
- Private constructor + `Of(T)` factory that calls `Validate(T)`
- Explicit cast to the primitive type
- `GetUnderlyingValue()` returning the underlying primitive
- `Equals`, `GetHashCode`, `ToString` delegating to the primitive
- Nested `JsonConverter` for System.Text.Json
- Optional `New()` factory via `[NewableValueObject]` (for snowflake IDs on `IValueObject<long>`)

The interface provides:
- **`static virtual void Validate(T value)`** — override to add custom validation (default is no-op)
- **`object IValueObject.GetUnderlyingValue()`** — default implementation forwarding to `T GetUnderlyingValue()`

```csharp
[NewableValueObject]
public readonly partial struct OrderId : IValueObject<long>;

public readonly partial struct CustomerName : IValueObject<string>
{
    public static void Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Customer name cannot be empty");
    }
}

// Usage
var id = OrderId.New();              // Snowflake ID
var name = CustomerName.Of("Alice"); // Validated
long raw = (long)id;                 // Explicit cast to primitive
long raw2 = id.GetUnderlyingValue(); // IValueObject<T> interface
```

The `IValueObject` / `IValueObject<T>` interfaces enable the source generator's `ValueObject ↔ primitive` conversion at compile time. See [design/value-object.md](/docs/design/value-object.md) for the full design rationale.

## Installation

```shell
dotnet add package NOF.Domain
```

## License

Apache-2.0
