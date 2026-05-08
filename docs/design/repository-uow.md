# Repository & Unit of Work

## Current Model

NOF does not provide a public repository abstraction or a framework-level unit-of-work API.

The current persistence model is:

1. application handlers work directly with `DbContext` or `NOFDbContext`
2. entity changes are tracked by EF Core
3. `SaveChangesAsync()` is the transaction boundary for both data and deferred outbox messages

## Why

This keeps the framework aligned with its actual runtime model:

- `NOF.Infrastructure` integrates with EF Core directly
- transactional messaging is tied to the active `DbContext`
- handlers already receive the concrete services they need through DI

Adding a separate repository or unit-of-work layer at the framework level would duplicate EF Core behavior and hide the real persistence boundary.

## Recommended Pattern

Use `DbContext` in application handlers:

```csharp
public sealed class GetOrder : OrderService.GetOrder
{
    private readonly DbContext _dbContext;

    public GetOrder(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task<Result<OrderDto>> HandleAsync(GetOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Set<Order>().FindAsync([request.Id], cancellationToken);
        if (order is null)
        {
            return Result.Fail("404", "Order not found");
        }

        return Result.Success(new OrderDto(order.Id, order.Status));
    }
}
```

For writes plus outbox dispatch:

```csharp
public sealed class CreateOrder : OrderService.CreateOrder
{
    private readonly DbContext _dbContext;
    private readonly INotificationPublisher _notificationPublisher;

    public CreateOrder(DbContext dbContext, INotificationPublisher notificationPublisher)
    {
        _dbContext = dbContext;
        _notificationPublisher = notificationPublisher;
    }

    public override async Task<Result> HandleAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = Order.Create(EmailAddress.Of(request.CustomerEmail));
        _dbContext.Set<Order>().Add(order);
        _notificationPublisher.DeferPublish(new OrderCreatedNotification(order.Id));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

## Notes

- You can still create app-specific repository classes if your application needs them.
- Those repositories are application design choices, not NOF framework contracts.
- For framework docs and samples, prefer showing `DbContext` directly so the persistence model stays accurate.
