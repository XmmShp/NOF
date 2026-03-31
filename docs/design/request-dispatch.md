# Request Dispatch Removal

## Decision

NOF no longer treats generic request-dispatch as the primary public path.
RPC invocation is centered on strong-typed service interfaces marked with `IRpcService`.

## Current Invocation Model

- RPC-style requests: call `IRpcService` methods directly.
- Command sending: unchanged.
- Notification publishing: unchanged.

## Notes

- Transport-specific HTTP client generation is now triggered by `[HttpServiceClient<TService>]` on partial classes.
- Service implementation splitting uses `[ServiceImplementation<TService>]` and runtime startup validation.
