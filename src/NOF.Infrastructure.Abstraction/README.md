# NOF.Infrastructure.Abstraction

Abstractions package for the NOF Framework infrastructure layer.

This package contains all interfaces, contracts, context types, pipeline types, entities, and options that are shared across infrastructure implementations. It has minimal dependencies and is designed to be referenced by both infrastructure implementations and application-level code that needs to interact with infrastructure contracts without pulling in heavy implementation dependencies.

## Contents

- **Builder contracts** — `INOFAppBuilder`, `IServiceRegistrationContext`, `IApplicationInitializationContext`
- **Step contracts** — `IStep`, `IAfter<T>`, `IBefore<T>`, `IServiceRegistrationStep`, `IApplicationInitializationStep`, and all predefined step marker interfaces
- **Pipeline contracts** — `IInboundMiddleware`, `IOutboundMiddleware`, `IInboundPipelineExecutor`, `IOutboundPipelineExecutor`, `InboundPipelineTypes`, `OutboundPipelineTypes`
- **Context types** — `InboundContext`, `OutboundContext`
- **Provided interfaces** — `ICommandRider`, `IRequestRider`, `INotificationRider`, `IEventPublisher`, `IIdentityResolver`, `ICacheSerializer`, `ICacheLockRetryStrategy`, `IOutboxMessageRepository`, `IInboxMessageRepository`, `ITenantRepository`
- **Consumed interfaces** — `IEndpointNameProvider`, `IStartupEventChannel`
- **Entities** — `OutboxMessage`, `InboxMessage`, `Tenant`, `HandlerInfo`, `HandlerInfos`
- **Options** — `CacheServiceOptions`, `OutboxOptions`, `ScopeAwareHttpClientFactoryOptions`
- **Default implementations** — `ApplicationInitializationStep`, `ServiceRegistrationStep`, `StartupEventChannel`, `EndpointNameProvider`, `JsonCacheSerializer`, `ExponentialBackoffCacheLockRetryStrategy`
- **Constants** — `NOFInfrastructureCoreConstants`

## Dependencies

- `NOF.Application`
- `Microsoft.Extensions.Hosting.Abstractions`
- `Microsoft.Extensions.Options.DataAnnotations`
