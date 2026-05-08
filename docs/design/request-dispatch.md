# Request Dispatch

## Decision

NOF keeps generic command and notification dispatch, but RPC-style request handling is centered on strongly typed `IRpcService` contracts and `RpcServer<TService>` implementations.

## Current Invocation Model

- RPC operations: call `IRpcService` methods through generated RPC clients or local server dispatch.
- Command sending: use `ICommandSender`.
- Notification publishing: use `INotificationPublisher`.
- Deferred dispatch: use `IDeferredCommandSender` and `IDeferredNotificationPublisher`.

## Notes

- HTTP exposure for RPC services is explicit via `app.MapHttpEndpoint<TRpcServer>()`.
- OpenAPI registration is always enabled by `NOFWebApplicationBuilder.Create(args)`, but `app.MapOpenApi()` remains an explicit host decision.
- Application implementations are built around `RpcServer<TService>` rather than ad-hoc request-dispatch APIs.
