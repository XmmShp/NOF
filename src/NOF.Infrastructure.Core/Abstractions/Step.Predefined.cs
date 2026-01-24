namespace NOF;

/// <summary>
/// Represents the base configuration for settings retrieval infrastructure.
/// Implementations register services that enable other components to
/// read application settings. This configuration is a prerequisite
/// for any service that depends on runtime configuration data.
/// </summary>
public interface IBaseSettingsServiceRegistrationStep : IServiceRegistrationStep;

/// <summary>
/// Represents a service configuration unit that depends on foundational services already being registered.
/// Implementations typically configure higher-level components (e.g., pipelines, decorators, clients)
/// that require base services to be available in the container.
/// </summary>
public interface IDependentServiceRegistrationStep : IServiceRegistrationStep, IAfter<IBaseSettingsServiceRegistrationStep>;

public interface IPublicDataSeedInitializationStep : IApplicationInitializationStep;

/// <summary>
/// Configures synchronous data seeding or initial state setup (e.g., database seeders, cache warm-up).
/// This is the first step in the application configuration pipeline.
/// </summary>

public interface IDataSeedInitializationStep : IApplicationInitializationStep, IAfter<IPublicDataSeedInitializationStep>;


/// <summary>
/// Configures observability infrastructure such as logging, metrics, and distributed tracing.
/// Depends on data seeding to ensure telemetry contexts are properly initialized.
/// </summary>

public interface IObservabilityInitializationStep : IApplicationInitializationStep, IAfter<IDataSeedInitializationStep>;


/// <summary>
/// Configures security-related middleware and policies (e.g., CORS).
/// Executes after observability to ensure security events are logged and traced.
/// </summary>

public interface ISecurityInitializationStep : IApplicationInitializationStep, IAfter<IObservabilityInitializationStep>;


/// <summary>
/// Configures global response formatting, error wrapping, and content negotiation.
/// Runs after security to ensure responses respect authentication/authorization outcomes.
/// </summary>

public interface IResponseFormattingInitializationStep : IApplicationInitializationStep, IAfter<ISecurityInitializationStep>;


/// <summary>
/// Configures authentication and authorization schemes (e.g., JWT, OpenID Connect, policy-based auth).
/// Depends on response formatting to ensure auth failures return standardized error structures.
/// </summary>

public interface IAuthenticationInitializationStep : IApplicationInitializationStep, IAfter<IResponseFormattingInitializationStep>;


/// <summary>
/// Configures business logic integration, such as domain event handlers,
/// or application service registrations that require authenticated context.
/// </summary>

public interface IBusinessLogicInitializationStep : IApplicationInitializationStep, IAfter<IAuthenticationInitializationStep>;


/// <summary>
/// Configures HTTP endpoints, route mappings, and minimal API definitions.
/// This is the final step in the application configuration pipeline,
/// ensuring all underlying infrastructure (auth, business logic, etc.) is ready.
/// </summary>

public interface IEndpointInitializationStep : IApplicationInitializationStep, IAfter<IBusinessLogicInitializationStep>;
