# Public API Overview

## Current Model

NOF RPC contracts are interface-first and method-based.

```csharp
public interface IUserService : IRpcService
{
    [HttpEndpoint(HttpVerb.Get, "/api/users/get")]
    Result<UserDto> GetUser(GetUserRequest request);

    [HttpEndpoint(HttpVerb.Get, "/api/users/watch")]
    StreamingResult<UserEvent> WatchUsers(WatchUsersRequest request);
}
```

## Service Interface Rules

- RPC interfaces implement `IRpcService`.
- Each RPC method takes exactly one request object.
- Service methods are synchronous on the contract surface: no `Task`, no `ValueTask`, no `CancellationToken`.
- Method overloading is not supported.
- `void` return types are not supported.
- Server-streaming methods return `StreamingResult<T>` on the contract surface.
- Route parameters are not supported for RPC HTTP endpoints; put the input data on the request object instead.

## Application Implementation Model

Application-side implementations use `RpcServer<TService>` and generated nested handler base classes:

```csharp
public partial class UserService : RpcServer<IUserService>;

public class GetUser : UserService.GetUser
{
    public override Task<Result<UserDto>> HandleAsync(GetUserRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new UserDto(request.Id, "Alice")));
    }
}
```

Streaming methods are implemented with the same generated handler model:

```csharp
public class WatchUsers : UserService.WatchUsers
{
    public override Task<StreamingResult<UserEvent>> HandleAsync(WatchUsersRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(StreamingResult.Success(Stream()));

        async IAsyncEnumerable<UserEvent> Stream()
        {
            yield return new UserEvent("connected");
            await Task.Delay(1000, cancellationToken);
            yield return new UserEvent("updated");
        }
    }
}
```

## HTTP Exposure

`NOF.Hosting.AspNetCore` maps registered RPC servers automatically during application initialization:

```csharp
builder.AddRpcServer<UserService>();
```

OpenAPI service registration happens during builder creation. Call `app.MapOpenApi()` explicitly when you want to expose the document.

When the mapped RPC method returns `StreamingResult<T>`, `NOF.Hosting.AspNetCore` exposes it as an SSE endpoint and generated HTTP clients consume it as `Task<StreamingResult<T>>`.

## RPC Transport Declaration

An RPC service contract can declare one intended transport:

```csharp
[TransportOverHttp(HttpRpcStyle.JsonRpc, "/rpc")]
public interface IUserService : IRpcService;
```

`TransportOverAttribute` is abstract and belongs to `NOF.Contract`, so callers and implementations read the same transport metadata from the RPC service interface. The analyzer rejects declarations on interfaces that do not inherit `IRpcService` and rejects multiple transport declarations on one contract. The route prefix is optional. During automatic endpoint registration, `ControllerRpc` prepends it to each operation route, while `JsonRpc` uses it as the single JSON-RPC endpoint route. A JSON-RPC contract without a route prefix is mapped at `/`.

Registering the server through `AddRpcServer<UserService>()` exposes it using the transport style and route prefix declared by the contract.

The same declaration generates both `IUserServiceClient` and a public partial `HttpUserServiceClient` in the contract assembly. The HTTP client constructor requires `HttpClient` and accepts `IRequestOutboundPipelineExecutor` optionally. This keeps the transport choice and usable client together: hosts with an outbound pipeline get middleware execution through DI, while lightweight callers can use the generated client without referencing `NOF.Hosting`.

Because the endpoint is bound to one RPC server, the JSON-RPC method key is only the contract operation name, such as `GetUser`. Request inbound middleware continues to run before the handler. Business `IResult` values are encoded as the JSON-RPC `result`; protocol failures are encoded as `error`.

Methods on a `JsonRpc` contract must not declare `[HttpEndpoint]`: JSON-RPC always uses the service-level route prefix and the RPC operation name. `ControllerRpc` methods may use `[HttpEndpoint]` to select their HTTP verb and operation route.

The first version supports unary calls and object `params` only. Batch calls, notifications, and streaming results are intentionally excluded.

## Diagnostics

Current RPC analyzer diagnostics include:

- `NOF200`: `[HttpEndpoint]` request type must be a reference type.
- `NOF201`: route parameters are not supported.
- `NOF202`: class request with explicit constructors must include a public parameterless constructor.
- `NOF207`: invalid RPC method signature.
- `NOF208`: service method overloads are not supported.
- `NOF209`: `void` return types are not supported.
- `NOF211`: a transport attribute can only be declared on an interface inheriting `IRpcService`.
- `NOF212`: an RPC service contract can declare at most one `TransportOverAttribute`.
- `NOF213`: methods on a JSON-RPC contract must not declare `[HttpEndpoint]`.
- `NOF300`: a class inheriting `RpcServer<TService>` must be `partial`.
