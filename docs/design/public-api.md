# Public API Overview

## Current Model (2026-03)

`[GenerateService]` is now method-driven.
The service interface itself is the RPC contract source of truth.

```csharp
[GenerateService]
public partial interface IUserService
{
    [HttpEndpoint(HttpVerb.Get, "/api/users/{id}")]
    Task<Result<UserDto>> GetUserAsync(GetUserRequest request, CancellationToken cancellationToken = default);
}
```

## GenerateService Rules

- `GenerateServiceAttribute` only keeps `GenerateHttpClient`.
- Service methods must be async only:
  - Return type must be `Task` or `Task<T>`.
- Service methods must have exactly one business parameter.
  - An optional `CancellationToken` parameter is allowed.
  - No second business parameter is allowed.

## Generated Output

For `IUserService`, NOF generates:

1. `HttpUserService` (if `GenerateHttpClient = true`).
2. ASP.NET endpoint mapping (`MapAllHttpEndpoints`) that resolves and calls `IUserService` directly.

No generated endpoint path uses `IRequestDispatcher` anymore.

### HTTP Route Behavior

- If a method has `[HttpEndpoint]`, the configured verb/route is used.
- If no `[HttpEndpoint]` exists, it defaults to:
  - Verb: `POST`
  - Route: method operation name (method name without `Async` suffix)

## Application-Layer Implementation Split

Use generic attribute on a partial class:

```csharp
[ServiceImplementation<IUserService>]
public partial class UserService : IUserService
{
}
```

Generator emits nested one-method interfaces on that class, for example:

```csharp
partial class UserService
{
    public interface IGetUser
    {
        Task<Result<UserDto>> GetUserAsync(GetUserRequest request, CancellationToken cancellationToken = default);
    }
}
```

This keeps one-service contract while enabling fine-grained implementation splitting/decoupling.

## Diagnostics

### Contract generator/analyzer

- `NOF200`: request type on `[HttpEndpoint]` must be reference type.
- `NOF201`: route parameter must match a public property.
- `NOF202`: class request with explicit constructors must include public parameterless constructor.
- `NOF207`: service method signature invalid (must be one business parameter + optional `CancellationToken`, and return `Task`/`Task<T>`).

### Application generator/analyzer

- `NOF300`: class with `[ServiceImplementation<TService>]` must be `partial`.
- `NOF301`: `TService` must be an interface marked with `[GenerateService]`.
- `NOF302`: class must implement `TService`.

## Removed Request Dispatch API

Request-style generic sender/dispatcher APIs are removed from public usage flow:

- Removed request send APIs from test host/scope.
- Removed `SendRequest` helpers in transport integration.
- Removed `IRequestDispatcher` infrastructure service and implementation.

The request RPC path is now fully represented by generated service interface calls.
Command and notification send/publish APIs are still preserved.
