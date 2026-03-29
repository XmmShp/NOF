# Request Dispatch Refactor

## Context

`IRequestSender` (Contract) and `IRequestRider` (Infrastructure transport rider) exposed generic request dispatch as an application-facing API.

This encouraged direct RPC-by-message usage in business/UI code instead of strong-typed service contracts.

## Decision

NOF now uses two distinct request invocation paths:

- Strong-typed service RPC:
  - Generated service interfaces (`[GenerateService]`) and `Http*Service` implementations.
  - Application/UI code should depend on these interfaces.
- In-process request dispatch:
  - Internal infrastructure service: `IRequestDispatcher`.
  - Used by hosting endpoint mapping and infrastructure components that need local request execution.

Removed:

- `NOF.Contract.IRequestSender`
- `NOF.Infrastructure.IRequestRider`
- `RequestSender`, `MemoryRequestRider`, `MassTransitRequestRider`

## In-Process Dispatch Model

`IRequestDispatcher` preserves NOF pipeline semantics:

1. Build `OutboundContext` and run outbound middleware (header propagation, tracing, tenant, etc.).
2. Resolve local handler via `IRequestHandlerResolver`.
3. Build `InboundContext` and run inbound middleware.
4. Execute handler and return `Result` / `Result<T>`.

This keeps behavior consistent with the previous sender+rider composition while removing generic dispatch from the Contract layer.

## Consequences

- Stronger architectural boundary:
  - Contract defines messages and strong-typed service contracts.
  - Infrastructure owns dispatch mechanics.
- Cross-process request calls should use HTTP strong-typed services.
- Framework internals and test utilities use `IRequestDispatcher` for local dispatch.
