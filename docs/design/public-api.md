# Public API Overview

## Current Model

NOF RPC contracts are now interface-first and marker-based.

```csharp
public partial interface IUserService : IRpcService
{
    [HttpEndpoint(HttpVerb.Get, "/api/users/{id}")]
    Task<Result<UserDto>> GetUserAsync(GetUserRequest request, CancellationToken cancellationToken = default);
}
```

## Service Interface Rules

- RPC interface must implement `IRpcService`.
- Methods must be async only: `Task` or `Task<T>`.
- Methods must have exactly one business parameter.
- Optional `CancellationToken` is allowed.

## Http Client Generation

`NOF.Contract.HttpServiceClientAttribute<TService>` is now the trigger for HTTP client code generation.
It is placed on a partial class (not on the service interface).

```csharp
[HttpServiceClient<IUserService>]
public partial class HttpUserService;
```

Generated class behavior:

- Class gets generated method bodies for `IUserService`.
- Class auto-implements `IUserService` in generated partial when needed.
- Route/verb come from method-level `[HttpEndpoint]`.
- If no `[HttpEndpoint]`, defaults to `POST` + operation-name route.

## Service Implementation Split (Application)

Use generic `ServiceImplementationAttribute<TService>` on a partial class:

```csharp
[ServiceImplementation<IUserService>]
public partial class UserService;
```

Generated result includes:

- Nested operation contracts, one per RPC method.
- Nested interface names do not start with `I` (for example `GetUser`).
- Service class gets generated forwarding methods and auto-implements `TService` when needed.

Forwarding resolves operation contracts from DI at runtime.

## Runtime Completeness Check

Compile-time strict completeness enforcement was removed for split implementations.
NOF now validates missing generated operation contracts during application startup.

## Diagnostics

### Contract analyzer

- `NOF200`: `[HttpEndpoint]` request type must be reference type.
- `NOF201`: route parameter must match a public property.
- `NOF202`: class request with explicit constructors must include a public parameterless constructor.
- `NOF207`: invalid RPC method signature.

### Application analyzer

- `NOF300`: class with `[ServiceImplementation<TService>]` must be `partial`.
- `NOF301`: `TService` must be an interface implementing `IRpcService`.

## Request Invocation Model

- RPC requests use strong-typed `IRpcService` interfaces.
- Command and notification APIs remain unchanged.
- Generic request-dispatch style APIs are no longer part of the main public usage path.
