# Public API: From One Attribute to Three

## The Problem with One Attribute

The original design used a single attribute — `[ExposeToHttpEndpoint(HttpVerb.Post, "/api/items")]` — to mark a request type as both a public API operation and an HTTP endpoint. The source generator scanned for this attribute, generated an HTTP client with methods for each endpoint, and generated ASP.NET Core minimal API route mappings.

This worked, but it conflated two concerns. Not every public operation needs an HTTP endpoint. Internal microservice calls, message bus operations, and in-process dispatches are all valid ways to invoke a request — and none of them need a route or an HTTP verb. Yet the only way to include an operation in the generated service interface was to slap an HTTP attribute on it, even if the operation would never be called over HTTP.

The naming was also awkward. A method called `CreateItemAsync` on a service interface has nothing inherently HTTP about it. The HTTP transport is an implementation detail of one of the generated clients. The interface method should exist because the operation is part of the public contract, not because it happens to have a REST endpoint.

## Three Attributes, Three Concerns

The refactored design separates the concerns into three attributes:

### `[PublicApi]`

Marks a request type as part of the public API surface. This is the minimum needed to include an operation in the generated service interface.

```csharp
[PublicApi]
public record CreateItemRequest(string Name) : IRequest<ItemDto>;
```

The attribute accepts an optional `OperationName` to override the default (which strips the `Request` suffix from the type name):

```csharp
[PublicApi(OperationName = "CreateItem")]
public record CreateItemCommand(string Name) : IRequest<ItemDto>;
```

### `[HttpEndpoint]`

Specifies the HTTP verb and route for a request type. This attribute requires `[PublicApi]` to be present — you cannot have an HTTP endpoint that is not part of the public API.

```csharp
[PublicApi]
[HttpEndpoint(HttpVerb.Post, "/api/items")]
public record CreateItemRequest(string Name) : IRequest<ItemDto>;
```

If a request has `[PublicApi]` but no `[HttpEndpoint]`, the generated HTTP client defaults to POST with the operation name as the route. This means every public API operation is callable over HTTP — it just might not have a curated REST endpoint.

### `[GenerateService]`

Placed on a `partial interface` to trigger code generation. This is the entry point for both the service generator and the endpoint mapper generator.

```csharp
[GenerateService]
public partial interface IMyService;
```

The attribute controls what gets generated:

- **`Namespaces`** — which namespaces to scan for `[PublicApi]` request types. Defaults to the interface's own namespace. Supports prefix matching, so `"MyApp"` includes `"MyApp.Commands"` and `"MyApp.Queries"`.
- **`GenerateHttpClient`** — whether to generate the `HttpClient`-based implementation. Default `true`.
- **`ExtraTypes`** — additional request types to include, regardless of namespace.

```csharp
[GenerateService(
    Namespaces = ["MyApp.Commands", "MyApp.Queries"],
    ExtraTypes = [typeof(SomeExternalRequest)])]
public partial interface IMyService;
```

## What Gets Generated

For a `[GenerateService]` interface named `IMyService`, the generator produces:

### 1. Interface methods

The `partial interface` is extended with a method for each `[PublicApi]` request type found in the scan namespaces:

```csharp
public partial interface IMyService
{
    Task<Result<ItemDto>> CreateItemAsync(CreateItemRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteItemAsync(DeleteItemRequest request, CancellationToken cancellationToken = default);
}
```

Method names are derived from the operation name plus `Async`. Return types are `Task<Result>` for `IRequest` and `Task<Result<T>>` for `IRequest<T>`. No `HttpCompletionOption` parameter — that is an HTTP concern and does not belong on the interface.

### 2. HTTP client (`HttpMyService`)

A `partial class` that implements the interface using `HttpClient`. Each method serializes the request, sends it to the appropriate endpoint, and deserializes the response.

- If the request has `[HttpEndpoint]`, the specified verb and route are used.
- If the request has only `[PublicApi]`, the method defaults to POST with the operation name as the route.
- Route parameters are extracted from the request's properties and substituted into the URL.
- All methods are `virtual`, allowing override in the partial class.

### 3. Endpoint mappings (`MapAllHttpEndpoints`)

The hosting-side generator produces an extension method on `WebApplication` that registers all `[PublicApi]` request types as minimal API endpoints. Like the HTTP client, requests without `[HttpEndpoint]` default to POST.

The mapper generator scans both the current compilation and all referenced assemblies for `[GenerateService]` interfaces, then applies the same namespace filtering logic as the service generator. This means the `[GenerateService]` interface can live in a Contract project while the mapper runs in the Hosting project.

## Conflict Detection

If you define a method on the partial interface yourself, the generator will not emit a conflicting method. Conflicts are detected by comparing method signatures in the form `MethodNameAsync(Full.Type.Name)`. This lets you override specific operations while letting the generator handle the rest.

## The Analyzer

Six diagnostic rules enforce correct usage:

| ID | Severity | Rule |
|----|----------|------|
| NOF200 | Error | Request type must be a reference type (class or record), not a struct |
| NOF201 | Error | Route parameter `{name}` has no matching public property on the request type |
| NOF202 | Error | Class request (not record) with `[HttpEndpoint]` must have a public parameterless constructor |
| NOF203 | Error | `OperationName` on `[PublicApi]` must be a valid C# identifier |
| NOF204 | Error | `[HttpEndpoint]` requires `[PublicApi]` to be present |
| NOF205 | Error | `ExtraTypes` entry must implement `IRequest` or `IRequest<T>` |
| NOF206 | Error | `ExtraTypes` entry must have `[PublicApi]` |

## In-Process Dispatch Design

`IRequestSender` and `IRequestRider` were removed. Request invocation now follows two clear paths:

- **Cross-process RPC**: call generated strong-typed HTTP services (`Http*Service`).
- **In-process dispatch**: use `IRequestDispatcher` (Infrastructure layer only).

`IRequestDispatcher` keeps the full NOF pipeline model:

- Runs the **outbound pipeline** first (header propagation, tracing, etc.).
- Resolves the local request handler via `IRequestHandlerResolver`.
- Executes the handler through the **inbound pipeline**.

This preserves existing middleware behavior for local calls while removing the contract-level generic sender abstraction. Application/UI code now uses strong-typed services instead of generic request dispatch APIs.

## Design Decisions

### Why default to POST?

When a `[PublicApi]` request has no `[HttpEndpoint]`, the HTTP client and mapper both default to POST with the operation name as the route. The alternative — throwing `NotSupportedException` — would mean the HTTP client interface has methods that blow up at runtime. Defaulting to POST means every method on the interface works. If you want a specific verb or route, add `[HttpEndpoint]`.

### Why scan by namespace?

Request types are discovered by scanning namespaces, not by being listed individually. This keeps the `[GenerateService]` attribute lightweight — you don't need to enumerate every request type. Namespace-prefix matching (`"MyApp"` includes `"MyApp.Sub"`) supports the common pattern where commands and queries live in sub-namespaces.

### Why partial and virtual?

Generated classes are `partial` so you can add custom methods, fields, or constructor logic. Generated methods are `virtual` so you can override specific operations — for example, to add custom headers or retry logic to one HTTP call without affecting the others.

### Why two generators?

The service generator (`ExposeToHttpEndpointServiceGenerator`) runs in the Contract project and produces the interface and HTTP client classes. The mapper generator (`ExposeToHttpEndpointMapperGenerator`) runs in the Hosting project and produces ASP.NET Core endpoint registrations. They are separate because they target different layers and different NuGet packages, but they share the same scanning logic and the same `[GenerateService]` trigger.
