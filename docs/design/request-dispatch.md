# Request Dispatch Removal

## Decision

NOF no longer exposes generic request-dispatch APIs in framework usage flow.
Request invocation is fully replaced by strong-typed RPC-style service interfaces generated from `[GenerateService]`.

## Removed Surface

- Contract-level request sender APIs.
- Framework-level request dispatcher abstractions/implementations.
- Request `SendAsync`/`SendRequest` helper methods in test and transport helper layers.

## Current Invocation Model

- Request-like operations: call generated service interface methods directly.
- Commands: keep command sender APIs.
- Notifications: keep publish APIs.

## Why

- Strong compile-time contract per service method.
- Clear architecture boundary: service contract first, transport second.
- Uniform async RPC semantics.
