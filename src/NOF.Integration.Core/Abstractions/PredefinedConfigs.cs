using Microsoft.Extensions.Hosting;

namespace NOF;

/// <summary>
/// Represents the base configuration for settings retrieval infrastructure.
/// Implementations register services that enable other components to
/// read application settings. This configuration is a prerequisite
/// for any service that depends on runtime configuration data.
/// </summary>
public interface IBaseSettingsServiceConfig : IServiceConfig;

/// <summary>
/// Represents a service configuration unit that depends on foundational services already being registered.
/// Implementations typically configure higher-level components (e.g., pipelines, decorators, clients)
/// that require base services to be available in the container.
/// </summary>
public interface IDependentServiceConfig : IServiceConfig, IDepsOn<IBaseSettingsServiceConfig>;

/// <summary>
/// Configures synchronous data seeding or initial state setup (e.g., database seeders, cache warm-up).
/// This is the first step in the application configuration pipeline.
/// </summary>
/// <typeparam name="THostApplication">The host application type, constrained to <see cref="IHost"/>.</typeparam>
public interface IDataSeedConfig<in THostApplication> : IApplicationConfig<THostApplication>
    where THostApplication : class, IHost;

/// <summary>
/// Configures observability infrastructure such as logging, metrics, and distributed tracing.
/// Depends on data seeding to ensure telemetry contexts are properly initialized.
/// </summary>
/// <typeparam name="THostApplication">The host application type, constrained to <see cref="IHost"/>.</typeparam>
public interface IObservabilityConfig<in THostApplication> : IApplicationConfig<THostApplication>, IDepsOn<IDataSeedConfig<THostApplication>>
    where THostApplication : class, IHost;

/// <summary>
/// Configures security-related middleware and policies (e.g., CORS).
/// Executes after observability to ensure security events are logged and traced.
/// </summary>
/// <typeparam name="THostApplication">The host application type, constrained to <see cref="IHost"/>.</typeparam>
public interface ISecurityConfig<in THostApplication> : IApplicationConfig<THostApplication>, IDepsOn<IObservabilityConfig<THostApplication>>
    where THostApplication : class, IHost;

/// <summary>
/// Configures global response formatting, error wrapping, and content negotiation.
/// Runs after security to ensure responses respect authentication/authorization outcomes.
/// </summary>
/// <typeparam name="THostApplication">The host application type, constrained to <see cref="IHost"/>.</typeparam>
public interface IResponseFormattingConfig<in THostApplication> : IApplicationConfig<THostApplication>, IDepsOn<ISecurityConfig<THostApplication>>
    where THostApplication : class, IHost;

/// <summary>
/// Configures authentication and authorization schemes (e.g., JWT, OpenID Connect, policy-based auth).
/// Depends on response formatting to ensure auth failures return standardized error structures.
/// </summary>
/// <typeparam name="THostApplication">The host application type, constrained to <see cref="IHost"/>.</typeparam>
public interface IAuthenticationConfig<in THostApplication> : IApplicationConfig<THostApplication>, IDepsOn<IResponseFormattingConfig<THostApplication>>
    where THostApplication : class, IHost;

/// <summary>
/// Configures business logic integration, such as domain event handlers,
/// or application service registrations that require authenticated context.
/// </summary>
/// <typeparam name="THostApplication">The host application type, constrained to <see cref="IHost"/>.</typeparam>
public interface IBusinessLogicConfig<in THostApplication> : IApplicationConfig<THostApplication>, IDepsOn<IAuthenticationConfig<THostApplication>>
    where THostApplication : class, IHost;

/// <summary>
/// Configures HTTP endpoints, route mappings, and minimal API definitions.
/// This is the final step in the application configuration pipeline,
/// ensuring all underlying infrastructure (auth, business logic, etc.) is ready.
/// </summary>
/// <typeparam name="THostApplication">The host application type, constrained to <see cref="IHost"/>.</typeparam>
public interface IEndpointConfig<in THostApplication> : IApplicationConfig<THostApplication>, IDepsOn<IBusinessLogicConfig<THostApplication>>
    where THostApplication : class, IHost;