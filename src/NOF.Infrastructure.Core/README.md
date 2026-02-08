# NOF.Infrastructure.Core

Core infrastructure package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the foundational infrastructure layer: the `INOFAppBuilder` application builder, the dependency-aware step pipeline for ordered service registration and application initialization, OpenTelemetry integration, and core service abstractions.

## Key Concepts

### Application Builder

`INOFAppBuilder` is the central entry point for configuring a NOF application. It orchestrates two phases:

1. **Service Registration** — `IServiceRegistrationStep` implementations register DI services
2. **Application Initialization** — `IApplicationInitializationStep` implementations configure the runtime pipeline

### Step Pipeline

Steps declare dependencies via `IAfter<T>` and `IBefore<T>`, and the framework executes them in topological order:

```csharp
public class MyDatabaseStep : IServiceRegistrationStep, IAfter<IBaseSettingsServiceRegistrationStep>
{
    public ValueTask ExecuteAsync(INOFAppBuilder builder)
    {
        // Register database services — guaranteed to run after settings are available
    }
}
```

### Predefined Steps

The framework provides ordered initialization phases:

`DataSeed` → `Observability` → `Security` → `ResponseFormatting` → `Authentication` → `BusinessLogic` → `Endpoints`

### OpenTelemetry

Built-in tracing, metrics, and logging via OTLP exporter, with HTTP instrumentation included.

## Installation

```shell
dotnet add package NOF.Infrastructure.Core
```

## License

Apache-2.0
