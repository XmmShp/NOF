# NOF Framework — AI Assistant Instructions

> **See also**: The `.agents/` directory contains organized rules, workflows, and skills for AI agents:
> - `.agents/rules/nof-dev.md` — Rules for developing the NOF framework itself
> - `.agents/rules/app-dev.md` — Rules for developing applications that USE NOF
> - `.agents/workflows/nof-dev/` — Step-by-step workflows for framework contributors
> - `.agents/workflows/app-dev/` — Step-by-step workflows for application developers
> - `.agents/skills/nof-app-development.md` — Comprehensive skill for building NOF applications

You are working in the **NOF (Neat Opinionated Framework)** repository, a modular .NET framework for building scalable applications with clean architecture, CQRS, and source generators.

## Repository Structure

```
src/
  NOF.Domain/                  — Domain entities, aggregate roots, events, [AutoInject]
  NOF.Contract/                — IRequest, ICommand, INotification, Result<T>, [ExposeToHttpEndpoint]
  NOF.Application/             — Handlers, state machines, caching, unit of work
  NOF.Infrastructure.Abstraction/ — INOFAppBuilder, IStep, shared abstractions
  NOF.Infrastructure.Core/     — App builder, step pipeline, OpenTelemetry, service wiring
  NOF.Domain.SourceGenerator/  — Source generator for [AutoInject], [ValueObject], SnowflakeId
  NOF.Contract.SourceGenerator/ — Source generator for [ExposeToHttpEndpoint], [Failure]
  NOF.Application.SourceGenerator/ — Source generator for handler registration
  Hostings/
    NOF.Hosting.AspNetCore/    — ASP.NET Core host, endpoint mapping, middleware, JSON config
    NOF.Hosting.AspNetCore.SourceGenerator/ — Endpoint mapper source generator
    NOF.Hosting.SourceGenerator/ — Hosting-level source generator
  Extensions/
    NOF.Infrastructure.Extension.Authorization.Jwt/ — JWT authority, OIDC, JWKS
  Infrastructures/
    NOF.Infrastructure.EntityFrameworkCore/          — NOFDbContext, repositories, outbox, multi-tenancy
    NOF.Infrastructure.EntityFrameworkCore.PostgreSQL/ — PostgreSQL provider
    NOF.Infrastructure.MassTransit/                  — MassTransit bus adapter
    NOF.Infrastructure.MassTransit.RabbitMQ/         — RabbitMQ transport
    NOF.Infrastructure.StackExchangeRedis/           — Redis caching
sample/                        — Sample application demonstrating NOF usage
tests/                         — Unit and integration tests
```

## Tech Stack

- **.NET 10** (C# 14, preview features enabled)
- **Central Package Management** via root `Directory.Packages.props`
- **Source Generators** using Microsoft.CodeAnalysis (Roslyn)
- **MassTransit** for messaging (RabbitMQ transport)
- **Entity Framework Core 10** with PostgreSQL
- **StackExchange.Redis** for caching
- **OpenTelemetry** for observability
- **xUnit** + **FluentAssertions** + **Moq** for testing
- **DocFX** for API documentation
- **GitHub Actions** for CI/CD

## Key Abstractions

### Messaging (CQRS)
- `IRequest` / `IRequest<TResponse>` — query/request messages
- `ICommand` — fire-and-forget command messages
- `INotification` — publish/subscribe event messages
- `IRequestHandler<T>` / `IRequestHandler<T, TResponse>` — handles requests
- `ICommandHandler<T>` — handles commands
- `INotificationHandler<T>` — handles notifications
- `IRequestSender`, `ICommandSender`, `INotificationPublisher` — dispatch abstractions
- `IDeferredNotificationPublisher`, `IDeferredCommandSender` — outbox-based deferred dispatch

### Builder Pipeline
- `INOFAppBuilder` — main builder interface, extends `IHostApplicationBuilder`
- `IServiceRegistrationStep` — runs during DI container setup
- `IApplicationInitializationStep` — runs after host is built, before start
- `IAfter<T>` / `IBefore<T>` — dependency ordering between steps
- Steps are executed in topological order based on declared dependencies

### Source Generator Attributes
- `[AutoInject(Lifetime)]` — auto-register class in DI container
- `[ExposeToHttpEndpoint(HttpVerb, route)]` — expose request as HTTP endpoint
- `[Failure]` — generate failure/error definitions
- `[ValueObject]` — generate value object boilerplate

### Domain
- `IRepository<T, TKey>` — repository abstraction with typed keys
- `IUnitOfWork` — transactional unit of work
- `IDeferredNotificationPublisher` / `IDeferredCommandSender` — outbox-based deferred dispatch

## Coding Conventions

### Style (enforced by .editorconfig + dotnet format)
- File-scoped namespaces (warning)
- Braces required for all control-flow blocks (warning)
- Allman-style braces (opening brace on new line)
- `var` when type is apparent
- Private instance fields: `_camelCase`
- Private static/readonly fields: `PascalCase`
- Constants: `PascalCase`
- All public APIs must have XML doc comments

### Project Rules
- `TreatWarningsAsErrors` is enabled for all `src/` projects
- Never specify NuGet `Version` in `.csproj` — use root `Directory.Packages.props`
- EF Core migrations (`**/Migrations/*.cs`) are excluded from formatting
- Commits follow Conventional Commits: `<type>(<scope>): <summary>`

## Build & Test Commands

```bash
dotnet restore                                    # Restore packages
dotnet build --configuration Release              # Build all projects
dotnet test                                       # Run all tests
dotnet format --verify-no-changes                 # Check formatting
dotnet pack src/<Project>/<Project>.csproj -o out  # Pack a NuGet package
```

## Usage Patterns for NOF Applications

### Application Bootstrap

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);
builder.Services.AddMyAppAutoInjectServices();  // Source-generated
builder.Services.AddAllHandlers();               // Source-generated
builder.AddRedisCache();
builder.AddJwtAuthority().AddJwksRequestHandler();
builder.AddJwtAuthorization();
builder.AddMassTransit().UseRabbitMQ();
builder.AddEFCore<AppDbContext>().AutoMigrate().UsePostgreSQL();
var app = await builder.BuildAsync();
app.MapAllHttpEndpoints();
await app.RunAsync();
```

### Value Objects (source-generated)

```csharp
[ValueObject<long>]
[NewableValueObject]  // Adds static New() for SnowflakeId
public readonly partial struct OrderId;

[ValueObject<string>]
public readonly partial struct EmailAddress
{
    private static void Validate(string input) { /* throw DomainException on invalid */ }
}
```

### Aggregate Roots & Domain Events

```csharp
public class Order : AggregateRoot
{
    public OrderId Id { get; init; }
    private Order() { }
    public static Order Create(string name)
    {
        var order = new Order { Id = OrderId.New(), Name = name };
        order.AddEvent(new OrderCreatedEvent(order.Id));
        return order;
    }
}
public record OrderCreatedEvent(OrderId Id) : IEvent;
```

### Repository Pattern

```csharp
// Domain layer — interface
public interface IOrderRepository : IRepository<Order, OrderId> { }

// Host project — EF Core implementation
[AutoInject(Lifetime.Scoped)]
public class OrderRepository : EFCoreRepository<Order, OrderId>, IOrderRepository
{
    public OrderRepository(DbContext dbContext) : base(dbContext) { }
}
```

### Typed Cache Keys

```csharp
public record OrderCacheKey(long Id) : CacheKey<OrderDto>($"Order:{Id}");
// Usage: await _cache.GetAsync(new OrderCacheKey(id), ct);
```

### PatchRequest with Optional Fields

```csharp
[ExposeToHttpEndpoint(HttpVerb.Patch, "api/orders/{id}")]
public record UpdateOrderRequest : PatchRequest, IRequest
{
    public long Id { get; init; }
    public Optional<string> Name { get => Get<string>(); set => Set(value); }
}
// Handler: request.Name.IfSome(n => order.UpdateName(n));
```

### Failure Definitions (source-generated)

```csharp
[Failure("OrderNotFound", "Order not found.", 404)]
[Failure("OrderAlreadyConfirmed", "Already confirmed.", 409)]
public static partial class OrderFailures;
// Usage: return Result.Fail(OrderFailures.OrderNotFound);
```

### State Machine

```csharp
public class OrderStateMachine : IStateMachineDefinition<OrderState>
{
    public void Build(IStateMachineBuilder<OrderState> builder)
    {
        builder.Correlate<OrderPlacedNotification>(n => $"Order-{n.OrderId}");
        builder.StartWhen<OrderPlacedNotification>(OrderState.Pending);
        builder.On(OrderState.Pending).When<PaymentReceived>().TransitionTo(OrderState.Processing);
    }
}
```

### Transactional Outbox

```csharp
_publisher.Publish(new OrderCreatedNotification(id));  // Deferred
await _uow.SaveChangesAsync(ct);  // Commits entity + outbox atomically
```

### Endpoint Metadata

| Attribute | Purpose |
|-----------|---------|
| `[ExposeToHttpEndpoint(HttpVerb, route)]` | Map to HTTP endpoint |
| `[AllowAnonymous]` | Skip authentication |
| `[Summary("...")]` | OpenAPI summary |
| `[EndpointDescription("...")]` | OpenAPI description |
| `[Category("...")]` | OpenAPI tag/group |

### Dispatch APIs

| Interface | Method | Description |
|-----------|--------|-------------|
| `IRequestSender` | `SendAsync(request, ct)` | Send request, get `Result<T>` |
| `ICommandSender` | `SendAsync(command, ct)` | Fire-and-forget command |
| `INotificationPublisher` | `PublishAsync(notification, ct)` | Broadcast notification |
| `IDeferredNotificationPublisher` | `Publish(notification)` | Outbox — published on `SaveChangesAsync()` |
| `IDeferredCommandSender` | `Send(command)` | Outbox — published on `SaveChangesAsync()` |

## When Making Changes

1. **Prefer minimal edits** — fix root causes, not symptoms.
2. **Follow existing patterns** — look at neighboring files for conventions.
3. **Add XML docs** for any new public API.
4. **Add or update tests** for behavioral changes.
5. **Never break the step pipeline** — ensure `IAfter<T>` / `IBefore<T>` dependencies are correct.
6. **Source generators** produce code at compile time — changes to generators require rebuilding consuming projects.
7. **Central package management** — add new NuGet dependencies to root `Directory.Packages.props`, not individual `.csproj` files.

## ⚠️ Complete Change Checklist

> **For both human developers and AI agents**: Every change to the NOF framework MUST consider ALL of the following. Do NOT only update source code.

- [ ] **Tests** — Add/update unit, integration, or source generator tests (`dotnet test`)
- [ ] **Sample** — Update `sample/` if APIs changed (must still compile)
- [ ] **Docs** — XML doc comments, `docs/`, `README.md`, `CONTRIBUTING.md` as needed
- [ ] **CI/CD** — Update `.github/workflows/` if new packages or test projects added
- [ ] **Agent instructions** — Update `.agents/` rules/workflows/skills and `.github/copilot-instructions.md`
- [ ] **Formatting** — `dotnet format --verify-no-changes` passes
- [ ] **Build** — `dotnet build --configuration Release` succeeds with no warnings
- [ ] **Commits** — Follow conventional commits: `<type>(<scope>): <summary>`
