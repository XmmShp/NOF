# NOF.Application

Application layer package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Contains the application service abstractions used to implement NOF applications: RPC servers, request handlers, command handlers, notification handlers, mapping, caching, object storage, and persistence contracts.

This package does not define runtime outbound authentication directives.

`AddNOFApplication()` registers the package-local application defaults, including expression-based mapping and the package-local Domain defaults.

Commands and notifications are plain payload types. Handler discovery comes from the `CommandHandler<T>` and `NotificationHandler<T>` base classes rather than marker interfaces on the message types.

## Key Abstractions

### RPC Servers

RPC contracts are declared on `IRpcService` interfaces in the contract layer. Application implementations use `RpcServer<TService>`:

```csharp
public partial class OrderService : RpcServer<IOrderService>;

using NOF.Application;

public sealed class GetOrder : OrderService.GetOrder
{
    private readonly IDbContext _dbContext;

    public GetOrder(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task<Result<OrderDto>> HandleAsync(GetOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Set<Order>()
            .FirstOrDefaultAsync(entity => entity.Id == request.Id, cancellationToken);
        if (order is null)
        {
            return Result.Fail("404", "Order not found");
        }

        return Result.Success(new OrderDto(order.Id, order.Status));
    }
}
```

When a host references `NOF.Infrastructure`, the same `OrderService` declaration also causes a `LocalOrderServiceClient` to be generated in the server namespace. Its service/client contract relationship comes from `IOrderServiceClient : IRpcClient<IOrderService>`, not from matching interface names.

Streaming RPC handlers use the same generated nested `RpcHandler<TRequest, StreamingResult<T>>` model:

```csharp
public partial class OrderService : RpcServer<IOrderService>;

public sealed class Watch : OrderService.Watch
{
    public override Task<StreamingResult<OrderEvent>> HandleAsync(WatchOrdersRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(StreamingResult.Success(Stream()));

        async IAsyncEnumerable<OrderEvent> Stream()
        {
            yield return new OrderEvent(Guid.NewGuid(), "Created");
            await Task.Delay(1000, cancellationToken);
            yield return new OrderEvent(Guid.NewGuid(), "Shipped");
        }
    }
}
```

The contract surface for the same method remains `StreamingResult<OrderEvent> Watch(WatchOrdersRequest request);`.

### Command Handlers

```csharp
public record SendEmailCommand(string To, string Subject, string Body);

public sealed class SendEmailHandler : CommandHandler<SendEmailCommand>
{
    public override Task HandleAsync(SendEmailCommand command, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

### Notification Handlers

```csharp
public record OrderCreatedNotification(Guid OrderId);

public sealed class OrderCreatedHandler : NotificationHandler<OrderCreatedNotification>
{
    public override Task HandleAsync(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

### Transactional Message Sending

Use `ICommandSender` and `INotificationPublisher` for both immediate and deferred dispatch:

```csharp
_notificationPublisher.DeferPublish(new OrderCreatedNotification(order.Id));
_commandSender.DeferSend(new SendEmailCommand(order.Email, "Created", "Order created."));
await _dbContext.SaveChangesAsync(cancellationToken);
```

### Persistence Abstractions

Application code should depend on `IDbContext` from `NOF.Application`, repository abstractions from `NOF.Domain`, and async query helpers under `NOF.Application` rather than a concrete ORM type.

```csharp
using NOF.Application;
using NOF.Domain;

var exists = await _dbContext.Set<Order>()
    .AsNoTracking()
    .AnyAsync(order => order.Id == request.Id, cancellationToken);
```

The async query surface is exposed through `IAsyncQueryable<T>` in `NOF.Domain` plus extension methods such as `AnyAsync`, `CountAsync`, `FirstOrDefaultAsync`, `SingleAsync`, `ToListAsync`, `SumAsync`, and `AverageAsync`. Concrete infrastructure adapters decide how those terminal operations are executed.

### Object Mapping (`IMapper`)

NOF uses expression-based mappings for both database projection and in-memory conversion:

```csharp
[Mappable<Order, OrderDto>]
[Mappable<Order, OrderSummary>]
public static partial class Mappings;
```

The generator writes `MappingRegistration` expressions into `MappingRegistry`.
Those mappings become available once the assembly is added via `AddApplicationPart(...)`. Every direction must be declared explicitly, and the generator reports an error instead of falling back to an opaque runtime mapping.

Apply a generated mapping directly to a query after filtering, ordering, and paging:

```csharp
var orders = await _dbContext.Set<Order>()
    .AsNoTracking()
    .Where(order => order.IsActive)
    .ProjectTo<OrderDto>()
    .ToListAsync(cancellationToken);
```

`ProjectTo<TDestination>()` receives an `IQueryable`, discovers the source type from `ElementType`, and resolves the expression through the ambient `Mapper.Current`. Each registration carries an AOT-safe generic query applicator, so no runtime generic method construction is required. Use `ProjectTo<TDestination>(mapper)` when an explicit mapper boundary is preferable.

Diagnostic `NOF025` warns when filtering, ordering, paging, set operations, another projection, or predicate-bearing terminal operations are composed after `ProjectTo`. Plain materialization and client-side work after `AsEnumerable` are not reported.

For an already materialized value, `IMapper.Map<TSource, TDestination>(source)` compiles and caches the same expression used by `ProjectTo`.
Custom pure expressions can be registered with `services.AddMapping<TSource, TDestination>(expression)`.

For package-local defaults:

```csharp
services.AddNOFApplication();
```

This registers `MappingRegistry`, the default `ExpressionMapper` implementation of `IMapper`, and a scoped daemon that binds `Mapper.Current` to the current async flow.

`AddNOFApplication()` already includes `AddNOFDomain()`:

```csharp
services.AddNOFApplication();
```

If you want to override the default `IIdGenerator`, register your own implementation explicitly:

```csharp
services.AddNOFApplication();
services.AddSingleton<IIdGenerator, MyIdGenerator>();
```

## Installation

```shell
dotnet add package NOF.Application
```

`ICacheService` also implements `IDistributedCache`, so standard distributed cache consumers can resolve either abstraction from NOF cache registrations.

### Object Storage

Application code can use `IObjectStorage` without depending on a cloud SDK. The common surface supports uploads, streaming reads, metadata, existence checks, deletes, server-side copies, and prefix-based enumeration:

```csharp
await using var content = File.OpenRead("invoice.pdf");
var stored = await objectStorage.PutAsync(
    "documents",
    $"invoices/{invoiceId}.pdf",
    content,
    new ObjectStorageWriteOptions { ContentType = "application/pdf" },
    cancellationToken);

var result = await objectStorage.OpenReadAsync(
    stored.BucketName,
    stored.ObjectKey,
    cancellationToken);
if (result.HasValue)
{
    await using var objectContent = result.Value.Content;
    // Stream objectContent to the caller or another destination.
}
```

The host selects an `IObjectStorageRider`; application handlers continue to depend only on `IObjectStorage`.

## License

Apache-2.0
