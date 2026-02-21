# NOF Common Recipes

## Table of Contents

- [Application Bootstrap](#application-bootstrap)
- [Value Objects](#value-objects)
- [Aggregate Root with Domain Events](#aggregate-root-with-domain-events)
- [Repository](#repository)
- [Request Handler (HTTP Endpoint)](#request-handler-http-endpoint)
- [PATCH with Optional Fields](#patch-with-optional-fields)
- [Failure Definitions](#failure-definitions)
- [Transactional Outbox](#transactional-outbox)
- [Typed Cache Keys](#typed-cache-keys)
- [State Machine](#state-machine)
- [Accessing User Identity](#accessing-user-identity)
- [DbContext Setup](#dbcontext-setup)

---

## Application Bootstrap

```csharp
using NOF.Hosting.AspNetCore;

var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);

// Source-generated registrations
builder.Services.AddMyAppAutoInjectServices();  // From [AutoInject]
builder.Services.AddAllHandlers();               // From source generator

// Infrastructure
builder.AddRedisCache();
builder.AddJwtAuthority().AddJwksRequestHandler();
builder.AddJwtAuthorization();
builder.AddMassTransit().UseRabbitMQ();
builder.AddEFCore<AppDbContext>().AutoMigrate().UsePostgreSQL();

var app = await builder.BuildAsync();
app.MapAllHttpEndpoints();
await app.RunAsync();
```

## Value Objects

```csharp
// Simple ID (SnowflakeId)
[ValueObject<long>]
[NewableValueObject]
public readonly partial struct OrderId;

// Validated value object
[ValueObject<string>]
public readonly partial struct EmailAddress
{
    private static void Validate(string input)
    {
        if (!input.Contains('@'))
            throw new DomainException(-1, "Invalid email format.");
    }
}

// Usage:
var id = OrderId.New();              // Generate SnowflakeId
var id = OrderId.Of(12345L);         // From primitive
long raw = (long)id;                 // Explicit cast to primitive
var email = EmailAddress.Of("a@b");  // Validated
```

## Aggregate Root with Domain Events

```csharp
public class Order : AggregateRoot
{
    public OrderId Id { get; init; }
    public string CustomerName { get; private set; }

    private Order() { }  // EF Core requires parameterless constructor

    public static Order Create(string customerName)
    {
        var order = new Order { Id = OrderId.New(), CustomerName = customerName };
        order.AddEvent(new OrderCreatedEvent(order.Id, customerName));
        return order;
    }

    public void UpdateName(string newName)
    {
        CustomerName = newName;
        AddEvent(new OrderUpdatedEvent(Id));
    }
}

public record OrderCreatedEvent(OrderId Id, string CustomerName) : IEvent;
public record OrderUpdatedEvent(OrderId Id) : IEvent;
```

## Repository

```csharp
// Domain layer — interface
public interface IOrderRepository : IRepository<Order, OrderId>
{
    Task<Order?> FindByCustomerAsync(string name, CancellationToken ct = default);
}

// Host project — EF Core implementation
[AutoInject(Lifetime.Scoped)]
public class OrderRepository : EFCoreRepository<Order, OrderId>, IOrderRepository
{
    public OrderRepository(DbContext dbContext) : base(dbContext) { }

    public async Task<Order?> FindByCustomerAsync(string name, CancellationToken ct)
        => await DbSet.FirstOrDefaultAsync(o => o.CustomerName == name, ct);
}
```

## Request Handler (HTTP Endpoint)

```csharp
// Contract
[ExposeToHttpEndpoint(HttpVerb.Get, "api/orders/{id}")]
[Summary("Get order by ID")]
[Category("Orders")]
public record GetOrderRequest(long Id) : IRequest<GetOrderResponse>;
public record GetOrderResponse(long Id, string CustomerName);

// Handler
public class GetOrderHandler : IRequestHandler<GetOrderRequest, GetOrderResponse>
{
    private readonly IOrderRepository _repo;
    public GetOrderHandler(IOrderRepository repo) => _repo = repo;

    public async Task<Result<GetOrderResponse>> HandleAsync(
        GetOrderRequest request, CancellationToken ct)
    {
        var order = await _repo.FindAsync(OrderId.Of(request.Id), ct);
        if (order is null)
            return Result.Fail(OrderFailures.OrderNotFound);

        return new GetOrderResponse((long)order.Id, order.CustomerName);
    }
}
```

## PATCH with Optional Fields

```csharp
[ExposeToHttpEndpoint(HttpVerb.Patch, "api/orders/{id}")]
public record UpdateOrderRequest : PatchRequest, IRequest
{
    public long Id { get; init; }
    public Optional<string> CustomerName { get => Get<string>(); set => Set(value); }
    public Optional<string?> Notes { get => Get<string?>(); set => Set(value); }
}

// In handler:
request.CustomerName.IfSome(name => order.UpdateName(name));
request.Notes.IfSome(notes => order.UpdateNotes(notes));
```

## Failure Definitions

```csharp
[Failure("OrderNotFound", "Order not found.", 404)]
[Failure("OrderAlreadyConfirmed", "Already confirmed.", 409)]
public static partial class OrderFailures;

// Usage: return Result.Fail(OrderFailures.OrderNotFound);
```

## Transactional Outbox

```csharp
public class CreateOrderHandler : IRequestHandler<CreateOrderRequest>
{
    private readonly IOrderRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IDeferredNotificationPublisher _publisher;

    public async Task<Result> HandleAsync(CreateOrderRequest request, CancellationToken ct)
    {
        var order = Order.Create(request.CustomerName);
        _repo.Add(order);

        _publisher.Publish(new OrderCreatedNotification((long)order.Id));  // Deferred
        await _uow.SaveChangesAsync(ct);  // Commits entity + outbox atomically

        return Result.Success();
    }
}
```

## Typed Cache Keys

```csharp
public record OrderCacheKey(long Id) : CacheKey<OrderDto>($"Order:{Id}");

// Usage:
var cached = await _cache.GetAsync(new OrderCacheKey(id), ct);
if (cached.HasValue) return cached.Value;

await _cache.SetAsync(new OrderCacheKey(id), dto, options, ct);
await _cache.RemoveAsync(new OrderCacheKey(id), ct);

// Get-or-set pattern:
var dto = await _cache.GetOrSetAsync(new OrderCacheKey(id), async ct => { ... }, options, ct);
```

## State Machine

```csharp
public class OrderStateMachine : IStateMachineDefinition<OrderState>
{
    public void Build(IStateMachineBuilder<OrderState> builder)
    {
        builder.Correlate<OrderPlacedNotification>(n => $"Order-{n.OrderId}");
        builder.StartWhen<OrderPlacedNotification>(OrderState.Pending);
        builder.On(OrderState.Pending)
            .When<PaymentReceivedNotification>()
            .TransitionTo(OrderState.Processing);
    }
}
```

## Accessing User Identity

```csharp
public class MyHandler : IRequestHandler<MyRequest>
{
    private readonly IInvocationContext _context;

    public async Task<Result> HandleAsync(MyRequest request, CancellationToken ct)
    {
        var user = _context.User;           // ClaimsPrincipal
        var tenantId = _context.TenantId;   // string?
        var traceId = _context.TraceId;     // string?
        // ...
    }
}
```

## DbContext Setup

```csharp
public class AppDbContext : NOFDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);  // REQUIRED: configures outbox/inbox tables
        // Configure entity mappings...
    }
}
```
