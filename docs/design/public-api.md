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

`NOF.Hosting.AspNetCore` maps RPC HTTP endpoints explicitly:

```csharp
app.MapHttpEndpoint<UserService>();
```

OpenAPI service registration happens during builder creation. Endpoint mapping stays in the host application, so call `app.MapOpenApi()` explicitly when you want to expose the document.

When the mapped RPC method returns `StreamingResult<T>`, `NOF.Hosting.AspNetCore` exposes it as an SSE endpoint and generated HTTP clients consume it as `Task<StreamingResult<T>>`.

## Diagnostics

Current RPC analyzer diagnostics include:

- `NOF200`: `[HttpEndpoint]` request type must be a reference type.
- `NOF201`: route parameters are not supported.
- `NOF202`: class request with explicit constructors must include a public parameterless constructor.
- `NOF207`: invalid RPC method signature.
- `NOF208`: service method overloads are not supported.
- `NOF209`: `void` return types are not supported.
- `NOF300`: a class inheriting `RpcServer<TService>` must be `partial`.
