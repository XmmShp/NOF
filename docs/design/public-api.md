# Public API Overview

## Current Model

NOF RPC contracts are interface-first and method-based.

```csharp
public interface IUserService : IRpcService
{
    [HttpEndpoint(HttpVerb.Get, "/api/users/get")]
    Result<UserDto> GetUser(GetUserRequest request);
}
```

## Service Interface Rules

- RPC interfaces implement `IRpcService`.
- Each RPC method takes exactly one request object.
- Service methods are synchronous on the contract surface: no `Task`, no `ValueTask`, no `CancellationToken`.
- Method overloading is not supported.
- `void` return types are not supported.
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

## HTTP Exposure

`NOF.Hosting.AspNetCore` maps RPC HTTP endpoints explicitly:

```csharp
app.MapHttpEndpoint<UserService>();
```

OpenAPI service registration happens during builder creation. Endpoint mapping stays in the host application, so call `app.MapOpenApi()` explicitly when you want to expose the document.

## Diagnostics

Current RPC analyzer diagnostics include:

- `NOF200`: `[HttpEndpoint]` request type must be a reference type.
- `NOF201`: route parameters are not supported.
- `NOF202`: class request with explicit constructors must include a public parameterless constructor.
- `NOF207`: invalid RPC method signature.
- `NOF208`: service method overloads are not supported.
- `NOF209`: `void` return types are not supported.
- `NOF300`: a class inheriting `RpcServer<TService>` must be `partial`.
