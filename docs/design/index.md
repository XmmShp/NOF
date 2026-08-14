# Design notes

NOF keeps its runtime surface small by moving repetitive framework work into source generators and explicit architectural building blocks. These notes explain the decisions behind those building blocks.

- [Request dispatch](request-dispatch.md) — how requests are routed through the framework.
- [Handler registration](handler-registration.md) — compile-time discovery and dependency registration.
- [Source generators](source-generators.md) — generated infrastructure and diagnostics.
- [Repository and unit of work](repository-uow.md) — persistence boundaries and transaction flow.
- [Value objects](value-object.md) — strongly typed domain values with generated support.
- [Mapping](mapper.md) — mapping conventions and generated implementations.
- [Steps](step.md) — composable application pipelines.
- [Public API](public-api.md) — compatibility and surface-area rules.

For types and members, continue to the [API reference](~/api/index.md).
