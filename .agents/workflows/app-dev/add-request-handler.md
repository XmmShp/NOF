---
description: Add a request/response operation using NOF RPC service contracts
---

# Add Request-Response Operation

NOF request-response is modeled via `IRpcService` contracts and `RpcServer<TService>` implementations.

## 1. Define Contract

```csharp
public interface IOrderService : IRpcService
{
    [HttpEndpoint(HttpVerb.Post, "api/orders/query")]
    Result<GetOrderResponse> GetOrder(GetOrderRequest request);
}

public record GetOrderRequest(string Id);
public record GetOrderResponse(string Id, string Name);
```

## 2. Define the RPC Server Container

```csharp
public partial class OrderService : RpcServer<IOrderService>;
```

## 3. Implement the Generated Handler Base

```csharp
public sealed class GetOrder : OrderService.GetOrder
{
    public override Task<Result<GetOrderResponse>> HandleAsync(GetOrderRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new GetOrderResponse(request.Id, "demo")));
    }
}
```

## 4. Wire in Program.cs

```csharp
builder.AddApplicationPart(typeof(OrderService).Assembly);
var app = await builder.BuildAsync();
app.MapHttpEndpoint<OrderService>();
```

## 5. Call from Other Components

- In-process: inject the generated local client or use the normal dispatch abstractions.
- Over HTTP: expose the RPC server with `MapHttpEndpoint<OrderService>()` and call the generated client from another NOF application.
