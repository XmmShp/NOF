---
description: How to add domain types in a NOF application using the current value-object and in-memory event model
---

# Add Domain Types

NOF currently models domain code around:

- value objects via `IValueObject<T>`
- generated IDs via `[NewableValueObject]`
- failure definitions via `[Failure(...)]`
- ordinary domain classes
- in-process domain events via `PublishAsEvent()`

## Add a Value Object

Value objects are immutable types wrapping a primitive. Implement `IValueObject<T>` and the source generator produces constructors, factory methods, JSON converters, equality, and casts.

```csharp
using NOF.Domain;

[NewableValueObject]
public readonly partial struct OrderId : IValueObject<long>;
```

For value objects with validation:

```csharp
using NOF.Domain;

public readonly partial struct EmailAddress : IValueObject<string>
{
    public static void Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Email cannot be empty.");
        }

        if (!value.Contains('@'))
        {
            throw new ValidationException("Invalid email format.");
        }
    }
}
```

## Add Failure Definitions

```csharp
using NOF.Domain;

[Failure("NotFound", "Order not found", "404001")]
[Failure("InvalidStatus", "Order status is invalid", "400002")]
public static partial class OrderFailures;
```

## Add a Domain Class

```csharp
using NOF.Abstraction;
using NOF.Domain;

public class Order
{
    public OrderId Id { get; init; }
    public EmailAddress CustomerEmail { get; private set; }
    public string Status { get; private set; }

    private Order() { }

    public static Order Create(EmailAddress customerEmail)
    {
        var order = new Order
        {
            Id = OrderId.New(),
            CustomerEmail = customerEmail,
            Status = "Pending"
        };

        new OrderCreatedEvent(order.Id, order.CustomerEmail).PublishAsEvent();
        return order;
    }

    public void Confirm()
    {
        if (Status == "Confirmed")
        {
            throw new DomainException(OrderFailures.InvalidStatus);
        }

        Status = "Confirmed";
        new OrderConfirmedEvent(Id).PublishAsEvent();
    }
}
```

## Add an In-Memory Event

```csharp
public record OrderCreatedEvent(OrderId Id, EmailAddress CustomerEmail);
public record OrderConfirmedEvent(OrderId Id);
```

## Handle the Event in Application Layer

```csharp
using NOF.Abstraction;

public sealed class OrderCreatedProjectionHandler : InMemoryEventHandler<OrderCreatedEvent>
{
    public override Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

## Persist with `DbContext`

Application handlers persist domain objects directly through `DbContext` / `NOFDbContext`:

```csharp
public sealed class CreateOrder : OrderService.CreateOrder
{
    private readonly DbContext _dbContext;

    public CreateOrder(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task<Result> HandleAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = Order.Create(EmailAddress.Of(request.CustomerEmail));
        _dbContext.Set<Order>().Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

## Notes

- Prefer ordinary domain classes plus value objects over nonexistent aggregate root base types.
- Raise in-process domain events with `PublishAsEvent()`.
- Handle those events with `InMemoryEventHandler<TEvent>` in the current DI scope.
- Persist changes through `DbContext` / `NOFDbContext` in the application layer.
- Value objects use explicit casts rather than a public `.Value` property.
