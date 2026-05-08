# NOF Recipes

## Program Bootstrap

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Hosting.AspNetCore;
using NOF.Infrastructure;
using NOF.Infrastructure.Extension.Authorization.Jwt;
using NOF.Infrastructure.RabbitMQ;
using NOF.Infrastructure.StackExchangeRedis;

var builder = NOFWebApplicationBuilder.Create(args);

builder.AddApplicationPart(typeof(MyAppService).Assembly);

builder.AddRedisCache(builder.Configuration.GetConnectionString("redis")
    ?? throw new InvalidOperationException("Connection string 'redis' not found."));
builder.AddJwtAuthority(o =>
{
    o.Issuer = "MyApp";
    o.SigningKeyEncryptionKey = builder.Configuration["NOF:Authority:SigningKeyEncryptionKey"]
        ?? throw new InvalidOperationException("Configuration value 'NOF:Authority:SigningKeyEncryptionKey' not found.");
});
builder.AddJwtResourceServer(o =>
{
    o.Issuer = "MyApp";
    o.RequireHttpsMetadata = false;
    o.JwksEndpoint = "http://localhost/.well-known/jwks.json";
});
builder.AddRabbitMQ(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
});

builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionString(builder.Configuration.GetConnectionString("postgres")
        ?? throw new InvalidOperationException("Connection string 'postgres' not found."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseNpgsql(connectionString))
    .MigrateOnInitialize();

var app = await builder.BuildAsync();
app.MapOpenApi();
app.MapHttpEndpoint<MyAppService>();
await app.RunAsync();
```

## RPC Contract + Implementation

```csharp
public interface IOrderService : IRpcService
{
    [HttpEndpoint(HttpVerb.Get, "api/orders/get")]
    Result<GetOrderResponse> GetOrder(GetOrderRequest request);
}

public partial class OrderService : RpcServer<IOrderService>;

public sealed class GetOrder : OrderService.GetOrder
{
    public override Task<Result<GetOrderResponse>> HandleAsync(GetOrderRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new GetOrderResponse(request.Id, "sample")));
    }
}
```

## Domain Update

```csharp
var order = await _dbContext.FindAsync<Order>([request.Id], cancellationToken);
order!.Confirm();
await _dbContext.SaveChangesAsync(cancellationToken);
```

## Access User/Tenant

```csharp
public sealed class MyHandler(IUserContext userContext, ITransparentInfos transparentInfos)
{
    public string? CurrentUserId => userContext.User.Id;
    public string CurrentTenant => transparentInfos.TenantId;
}
```

## Deferred Outbox Dispatch

```csharp
_notificationPublisher.DeferPublish(new OrderCreatedNotification(order.Id));
await _dbContext.SaveChangesAsync(cancellationToken);
```
