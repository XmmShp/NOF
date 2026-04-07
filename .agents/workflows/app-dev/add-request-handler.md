---
description: Add a request/response operation using NOF RPC service contracts
---

# Add Request-Response Operation

NOF request-response is modeled via `IRpcService` contracts and generated service implementations.

## 1. Define Contract

```csharp
[GenerateService]
public partial interface IOrderService : IRpcService
{
    [PublicApi]
    [HttpEndpoint(HttpVerb.Post, "api/orders/query")]
    Task<Result<GetOrderResponse>> GetOrderAsync(GetOrderRequest request, CancellationToken cancellationToken = default);
}

public record GetOrderRequest(string Id);
public record GetOrderResponse(string Id, string Name);
```

## 2. Implement Generated Base Type

```csharp
public sealed class GetOrder : OrderService.GetOrder
{
    public override async Task<Result<GetOrderResponse>> GetOrderAsync(GetOrderRequest request, CancellationToken cancellationToken)
    {
        // query repository...
        return new GetOrderResponse(request.Id, "demo");
    }
}
```

## 3. Wire in Program.cs

```csharp
builder.AddApplicationPart(typeof(IOrderService).Assembly);
var app = await builder.BuildAsync();
app.MapServiceToHttpEndpoints<IOrderService>();
```

## 4. Call from Other Components

- In-process: inject generated service implementation/client.
- Cross-service: use generated HTTP service client.
