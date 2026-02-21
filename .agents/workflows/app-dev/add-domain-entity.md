---
description: How to add a domain entity, aggregate root, value objects, and domain events in a NOF application
---

# Add a Domain Entity

NOF follows DDD patterns with aggregate roots, entities, value objects, and domain events.

## Adding a Value Object

Value objects are immutable types wrapping a primitive. The source generator produces constructors, factory methods, JSON converters, equality, and casts.

1. Create a file in `Domain/ValueObjects/`:
   ```csharp
   using NOF.Domain;

   [ValueObject<long>]
   [NewableValueObject]  // Adds a static New() method (for SnowflakeId generation)
   public readonly partial struct OrderId;
   ```

2. For value objects with validation:
   ```csharp
   using NOF.Domain;

   [ValueObject<string>]
   public readonly partial struct EmailAddress
   {
       private static void Validate(string input)
       {
           if (string.IsNullOrWhiteSpace(input))
           {
               throw new DomainException(-1, "Email cannot be empty.");
           }

           if (!input.Contains('@'))
           {
               throw new DomainException(-1, "Invalid email format.");
           }
       }
   }
   ```

3. Usage:
   ```csharp
   var id = OrderId.New();           // SnowflakeId (requires [NewableValueObject])
   var id = OrderId.Of(12345L);      // From primitive
   long raw = (long)id;              // Back to primitive
   var email = EmailAddress.Of("a@b.com");  // Validated
   ```

## Adding a Domain Event

1. Create a file in `Domain/Events/`:
   ```csharp
   using NOF.Domain;

   public record OrderCreatedEvent(OrderId Id, string CustomerName) : IEvent;
   ```

## Adding an Aggregate Root

1. Create a file in `Domain/AggregateRoots/`:
   ```csharp
   using NOF.Domain;

   public class Order : AggregateRoot
   {
       public OrderId Id { get; init; }
       public string CustomerName { get; private set; }
       public OrderStatus Status { get; private set; }

       private Order() { }  // EF Core requires parameterless constructor

       public static Order Create(string customerName)
       {
           var order = new Order
           {
               Id = OrderId.New(),
               CustomerName = customerName,
               Status = OrderStatus.Pending
           };

           order.AddEvent(new OrderCreatedEvent(order.Id, customerName));
           return order;
       }

       public void Confirm()
       {
           Status = OrderStatus.Confirmed;
           AddEvent(new OrderConfirmedEvent(Id));
       }
   }
   ```

## Adding a Child Entity

For entities owned by an aggregate root, use `Entity` base class and optionally `[Snapshotable]` for read-only snapshots:

```csharp
using NOF.Domain;

[Snapshotable]  // Generates a <ClassName>Snapshot record with all public properties
public class OrderItem : Entity
{
    public string ProductName { get; init; }
    public int Quantity { get; private set; }

    internal OrderItem() { }

    public OrderItem(string productName, int quantity)
    {
        ProductName = productName;
        Quantity = quantity;
    }
}
```

## Adding a Repository Interface

1. Create a file in `Domain/Repositories/`:
   ```csharp
   using NOF.Domain;

   public interface IOrderRepository : IRepository<Order, OrderId>
   {
       Task<Order?> FindByCustomerAsync(string customerName, CancellationToken ct = default);
   }
   ```

2. Implement in the host project (EF Core):
   ```csharp
   using NOF.Infrastructure.EntityFrameworkCore;

   [AutoInject(Lifetime.Scoped)]
   public class OrderRepository : EFCoreRepository<Order, OrderId>, IOrderRepository
   {
       public OrderRepository(DbContext dbContext) : base(dbContext) { }

       public async Task<Order?> FindByCustomerAsync(string customerName, CancellationToken ct)
           => await DbSet.FirstOrDefaultAsync(o => o.CustomerName == customerName, ct);
   }
   ```

## Adding Failure Definitions

Use `[Failure]` to generate static `Failure` instances:

```csharp
using NOF.Domain;

[Failure("OrderNotFound", "Order not found.", 404)]
[Failure("OrderAlreadyConfirmed", "Order is already confirmed.", 409)]
public static partial class OrderFailures;

// Usage in handler:
// return Result.Fail(OrderFailures.OrderNotFound);
```

## Notes

- Aggregate roots raise domain events via `AddEvent()` — events are dispatched when `IUnitOfWork.SaveChangesAsync()` is called.
- `[Snapshotable]` generates a read-only `record` snapshot — expose via `entity.ToSnapshot()`.
- `[NewableValueObject]` requires the SnowflakeId generator to be configured (it is by default in `NOFAppBuilder`).
- Value objects use explicit casts, not implicit — `(long)orderId` to get the primitive.
- Always keep the parameterless constructor `private` or `internal` for EF Core compatibility.
