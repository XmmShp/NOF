# Source Generator Isolation

Every NOF source generator treats generator output as unavailable input. A generator may inspect only user-authored declarations and stable types from referenced runtime assemblies.

Generated types may still be referenced in emitted source, but their names and signatures must be derived independently from the same user-authored declaration. Generators must never query another generator's emitted symbol, members, attributes, interfaces, or base types through Roslyn's semantic model.

The current generator inputs are:

| Package | Generator | Stable input |
| --- | --- | --- |
| Abstraction | `AutoInjectGenerator` | User class and `AutoInjectAttribute` |
| Abstraction | `EventHandlerRegistrationGenerator` | User class and `InMemoryEventHandler<T>` |
| Domain | `FailureGenerator` | User type and `FailureAttribute` |
| Domain | `ValueObjectGenerator` | User struct, `IValueObject<T>`, and `ValueObjectLengthAttribute` |
| Application | `HandlerRegistrationGenerator` | User class and the stable command/notification handler bases |
| Application | `MappableGenerator` | User mapping declaration, attributes, and mapped source/destination types |
| Application | `RpcServerGenerator` | User `RpcServer<TService>` and RPC service contract |
| Application | `RpcServerAutoInjectGenerator` | User `Server.Operation` base-type syntax, `RpcServer<TService>`, and RPC service contract |
| Contract | `RpcServiceClientGenerator` | User RPC service contract |
| Contract | `HttpRpcClientGenerator` | User RPC service contract and its transport metadata |
| Infrastructure | `LocalRpcClientGenerator` | User or referenced `RpcServer<TService>` and RPC service contract |

RPC client names are maintained in one shared source-generator convention. Contract, HTTP, and Local generators all calculate their output from the service/server names without inspecting each other's generated symbols. The generated client interface still implements `IRpcClient<TService>`, so the C# compiler verifies the service-to-client relationship in the final compilation.

The regression suite runs every generator together in one compilation in both normal and reversed order. This represents a monolithic application that references every NOF package and prevents generator ordering from becoming an implicit contract.

## Code Fixes

NOF runtime packages ship a shared `NOF.CodeFixes` analyzer assembly beside their layer-specific source generator. A code fix is offered only when the diagnostic has a deterministic local transformation, including required type modifiers, invalid or duplicate attribute removal, value-object primitive ordering casts, daemon-service resolution, supported RPC request/signature corrections, direct `DbContext` base replacement, NOF host build routing, and value-object length migration.

Diagnostics that require a domain or API design choice deliberately remain analyzer-only. Examples include choosing between RPC transports, renaming overloaded operations, selecting a parsable query representation, inventing unique failure names/codes, or rewriting `Find` calls without a known key predicate.
