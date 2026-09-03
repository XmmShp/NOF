# NOF Recipes

## Program Bootstrap

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Hosting;
using NOF.Hosting.AspNetCore;
using NOF.Infrastructure.EntityFrameworkCore;

var builder = NOFWebApplicationBuilder.Create(args);

builder.AddApplicationPart(typeof(OrderService).Assembly);
builder.AddRpcServer<OrderService>();

builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionString(builder.Configuration.GetConnectionString("postgres")
        ?? throw new InvalidOperationException("Connection string 'postgres' not found."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseNpgsql(connectionString))
    .MigrateOnInitialize();

var app = await builder.BuildAsync();
app.MapOpenApi();
await app.RunAsync();
```

`BuildAsync()` runs initialization steps and maps registered RPC servers according to the transport declared on each contract.

## Controller-Style RPC Contract + Handler

```csharp
using NOF.Application;
using NOF.Contract;

[TransportOverHttp(HttpRpcStyle.ControllerRpc)]
public interface IOrderService : IRpcService
{
    [HttpEndpoint(HttpVerb.Get, "api/orders/get")]
    Result<GetOrderResponse> GetOrder(GetOrderRequest request);
}

public partial class OrderService : RpcServer<IOrderService>;

public sealed class GetOrder : OrderService.GetOrder
{
    public override Task<Result<GetOrderResponse>> HandleAsync(
        GetOrderRequest request,
        Context context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new GetOrderResponse(request.Id, "sample")));
    }
}
```

## Provider-Neutral Query

```csharp
[Mappable<Order, GetOrderResponse>]
public static partial class Mappings;

public sealed class GetOrder(IDbContext dbContext) : OrderService.GetOrder
{
    public override async Task<Result<GetOrderResponse>> HandleAsync(
        GetOrderRequest request,
        Context context,
        CancellationToken cancellationToken)
    {
        var response = await dbContext.Set<Order>()
            .AsNoTracking()
            .Where(order => order.Id == request.Id)
            .ProjectTo<GetOrderResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result.Fail("404", "Order not found")
            : Result.Success(response);
    }
}
```

Register the source/destination pair with `[Mappable<,>]`, then filter, order, and page before `ProjectTo<TDestination>()`.

## Domain Update

```csharp
var order = await dbContext.Set<Order>()
    .FirstOrDefaultAsync(entity => entity.Id == request.Id, cancellationToken);

order!.Confirm();
await dbContext.SaveChangesAsync(cancellationToken);
```

## Access User and Tenant

```csharp
public sealed class MyHandler(IUserContext userContext, ICurrentTenant currentTenant)
{
    public string? CurrentUserId => userContext.User.Id;
    public string CurrentTenantId => currentTenant.TenantId;
}
```

## Immediate and Deferred Messaging

```csharp
await commandSender.SendAsync(command, context, cancellationToken);
await notificationPublisher.PublishAsync(notification, context, cancellationToken);

await notificationPublisher.DeferPublishAsync(
    new OrderCreatedNotification(order.Id),
    context,
    cancellationToken);
await dbContext.SaveChangesAsync(cancellationToken);
```

Deferred messages are inserted into the current `IDbContext`; saving that same context is the transaction boundary.

## Generated Client Call

```csharp
var result = await orderServiceClient.GetOrderAsync(request, context, cancellationToken);
```

HTTP and local implementations share the generated client interface and explicit `Context` parameter.
