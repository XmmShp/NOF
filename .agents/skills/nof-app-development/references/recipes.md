# NOF Recipes

## Program Bootstrap

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaults: true);

builder.AddApplicationPart(typeof(MyAppService).Assembly);

builder.AddRedisCache();
builder.AddJwtAuthority(o => o.Issuer = "MyApp");
builder.AddJwtResourceServer(o =>
{
    o.Issuer = "MyApp";
    o.RequireHttpsMetadata = false;
    o.JwksEndpoint = "http://localhost/.well-known/jwks.json";
});
builder.AddRabbitMQ();
builder.AddEFCore<AppDbContext>().UseSharedDatabaseTenancy().AutoMigrate().UsePostgreSQL();

var app = await builder.BuildAsync();
app.MapServiceToHttpEndpoints<IMyAppService>();
await app.RunAsync();
```

## RPC Contract + Implementation

```csharp
[GenerateService]
public partial interface IOrderService : IRpcService
{
    [PublicApi]
    [HttpEndpoint(HttpVerb.Get, "api/orders/{id}")]
    Task<Result<GetOrderResponse>> GetOrderAsync(GetOrderRequest request, CancellationToken cancellationToken = default);
}

public sealed class GetOrder : OrderService.GetOrder
{
    public Task<Result<GetOrderResponse>> GetOrderAsync(GetOrderRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new GetOrderResponse(request.Id, "sample")));
    }
}
```

## Domain Update

```csharp
order.UpdateName(request.Name);
_uow.Update(order);
await _uow.SaveChangesAsync(cancellationToken);
```

## Access User/Tenant

```csharp
public sealed class MyHandler(IUserContext userContext, IExecutionContext executionContext)
{
    public string? CurrentUserId => userContext.Id;
    public string CurrentTenant => executionContext.TenantId;
}
```

## Deferred Outbox Dispatch

```csharp
_deferredNotificationPublisher.Publish(new OrderCreatedNotification(order.Id));
await _uow.SaveChangesAsync(cancellationToken);
```
