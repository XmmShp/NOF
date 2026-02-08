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

### `[Snapshotable]`

Marks an aggregate root for snapshot-based persistence optimization.

## Installation

```shell
dotnet add package NOF.Domain
```

## License

Apache-2.0
