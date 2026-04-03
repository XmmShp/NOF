using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

/// <summary>
/// Represents the base configuration for settings retrieval infrastructure.
/// Implementations register services that enable other components to
/// read application settings. This configuration is a prerequisite
/// for any service that depends on runtime configuration data.
/// </summary>
public interface IBaseSettingsServiceRegistrationStep : IServiceRegistrationStep;

/// <summary>
/// CRTP variant of <see cref="IBaseSettingsServiceRegistrationStep"/> that automatically provides the
/// <see cref="IStep.Type"/> implementation via <see cref="IStep{TSelf}"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
public interface IBaseSettingsServiceRegistrationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : IBaseSettingsServiceRegistrationStep, IStep<TSelf>
    where TSelf : IBaseSettingsServiceRegistrationStep<TSelf>;

/// <summary>
/// Represents a service configuration unit that depends on foundational services already being registered.
/// Implementations typically configure higher-level components (e.g., pipelines, decorators, clients)
/// that require base services to be available in the container.
/// </summary>
public interface IDependentServiceRegistrationStep : IServiceRegistrationStep, IAfter<IBaseSettingsServiceRegistrationStep>;

/// <summary>
/// CRTP variant of <see cref="IDependentServiceRegistrationStep"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
public interface IDependentServiceRegistrationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : IDependentServiceRegistrationStep, IStep<TSelf>
    where TSelf : IDependentServiceRegistrationStep<TSelf>;

/// <summary>
/// Declares a handler middleware type to be included in the handler pipeline.
/// Extends <see cref="IServiceRegistrationStep"/> so each middleware step participates
/// in the normal service registration phase its <see cref="IServiceRegistrationStep.ExecuteAsync"/>
/// registers the middleware type as scoped in DI and appends it to the ordered pipeline type list.
/// <para>
/// Middleware ordering is resolved via <see cref="IAfter{T}"/> / <see cref="IBefore{T}"/>
/// on the concrete step class, using the same dependency graph as other registration steps.
/// The topological execution order of steps IS the pipeline order.
/// </para>
/// </summary>
public interface IInboundMiddlewareStep : IServiceRegistrationStep, IAfter<IDependentServiceRegistrationStep>
{
    /// <summary>
    /// The concrete <see cref="IInboundMiddleware"/> type this step contributes to the pipeline.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type MiddlewareType { get; }

    /// <summary>
    /// Default implementation: registers the middleware as scoped in DI
    /// and appends it to the <see cref="InboundPipelineTypes"/> ordered list.
    /// </summary>
    ValueTask IServiceRegistrationStep.ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.TryAddScoped(MiddlewareType);
        builder.Services.GetOrAddSingleton<InboundPipelineTypes>().Add(MiddlewareType);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// CRTP variant of <see cref="IInboundMiddlewareStep"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
public interface IInboundMiddlewareStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : IInboundMiddlewareStep, IStep<TSelf>
    where TSelf : IInboundMiddlewareStep<TSelf>;

/// <summary>
/// Strongly-typed variant of <see cref="IInboundMiddlewareStep"/> that infers
/// <see cref="IInboundMiddlewareStep.MiddlewareType"/> from <typeparamref name="TMiddleware"/>
/// and provides <see cref="IStep.Type"/> via CRTP.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
/// <typeparam name="TMiddleware">The concrete <see cref="IInboundMiddleware"/> type.</typeparam>
public interface IInboundMiddlewareStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>
    : IInboundMiddlewareStep<TSelf>
    where TSelf : IInboundMiddlewareStep<TSelf, TMiddleware>
    where TMiddleware : IInboundMiddleware
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type IInboundMiddlewareStep.MiddlewareType => typeof(TMiddleware);
}

/// <summary>
/// Declares an outbound middleware type to be included in the outbound pipeline.
/// Mirrors <see cref="IInboundMiddlewareStep"/> for the outbound direction.
/// The topological execution order of steps IS the outbound pipeline order.
/// </summary>
public interface IOutboundMiddlewareStep : IServiceRegistrationStep, IAfter<IDependentServiceRegistrationStep>
{
    /// <summary>
    /// The concrete <see cref="IOutboundMiddleware"/> type this step contributes to the pipeline.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type MiddlewareType { get; }

    /// <summary>
    /// Default implementation: registers the middleware as scoped in DI
    /// and appends it to the <see cref="OutboundPipelineTypes"/> ordered list.
    /// </summary>
    ValueTask IServiceRegistrationStep.ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.TryAddScoped(MiddlewareType);
        builder.Services.GetOrAddSingleton<OutboundPipelineTypes>().Add(MiddlewareType);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// CRTP variant of <see cref="IOutboundMiddlewareStep"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
public interface IOutboundMiddlewareStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : IOutboundMiddlewareStep, IStep<TSelf>
    where TSelf : IOutboundMiddlewareStep<TSelf>;

/// <summary>
/// Strongly-typed variant of <see cref="IOutboundMiddlewareStep"/> that infers
/// <see cref="IOutboundMiddlewareStep.MiddlewareType"/> from <typeparamref name="TMiddleware"/>
/// and provides <see cref="IStep.Type"/> via CRTP.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
/// <typeparam name="TMiddleware">The concrete <see cref="IOutboundMiddleware"/> type.</typeparam>
public interface IOutboundMiddlewareStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>
    : IOutboundMiddlewareStep<TSelf>
    where TSelf : IOutboundMiddlewareStep<TSelf, TMiddleware>
    where TMiddleware : IOutboundMiddleware
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type IOutboundMiddlewareStep.MiddlewareType => typeof(TMiddleware);
}

/// <summary>
/// Configures synchronous data seeding or initial state setup (e.g., database seeders, cache warm-up).
/// This is the first step in the application configuration pipeline.
/// </summary>
public interface IDataSeedInitializationStep : IApplicationInitializationStep;

/// <summary>
/// CRTP variant of <see cref="IDataSeedInitializationStep"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
public interface IDataSeedInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : IDataSeedInitializationStep, IStep<TSelf>
    where TSelf : IDataSeedInitializationStep<TSelf>;

/// <summary>
/// Configures observability infrastructure such as logging, metrics, and distributed tracing.
/// Depends on data seeding to ensure telemetry contexts are properly initialized.
/// </summary>
public interface IObservabilityInitializationStep : IApplicationInitializationStep, IAfter<IDataSeedInitializationStep>;

/// <summary>
/// CRTP variant of <see cref="IObservabilityInitializationStep"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
public interface IObservabilityInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : IObservabilityInitializationStep, IStep<TSelf>
    where TSelf : IObservabilityInitializationStep<TSelf>;

/// <summary>
/// Configures security-related middleware and policies (e.g., CORS).
/// Executes after observability to ensure security events are logged and traced.
/// </summary>
public interface ISecurityInitializationStep : IApplicationInitializationStep, IAfter<IObservabilityInitializationStep>;

/// <summary>
/// CRTP variant of <see cref="ISecurityInitializationStep"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
public interface ISecurityInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : ISecurityInitializationStep, IStep<TSelf>
    where TSelf : ISecurityInitializationStep<TSelf>;

/// <summary>
/// Configures global response formatting, error wrapping, and content negotiation.
/// Runs after security to ensure responses respect authentication/authorization outcomes.
/// </summary>
public interface IResponseFormattingInitializationStep : IApplicationInitializationStep, IAfter<ISecurityInitializationStep>;

/// <summary>
/// CRTP variant of <see cref="IResponseFormattingInitializationStep"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
public interface IResponseFormattingInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : IResponseFormattingInitializationStep, IStep<TSelf>
    where TSelf : IResponseFormattingInitializationStep<TSelf>;

/// <summary>
/// Configures authentication and authorization schemes (e.g., JWT, OpenID Connect, policy-based auth).
/// Depends on response formatting to ensure auth failures return standardized error structures.
/// </summary>
public interface IAuthenticationInitializationStep : IApplicationInitializationStep, IAfter<IResponseFormattingInitializationStep>;

/// <summary>
/// CRTP variant of <see cref="IAuthenticationInitializationStep"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
public interface IAuthenticationInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : IAuthenticationInitializationStep, IStep<TSelf>
    where TSelf : IAuthenticationInitializationStep<TSelf>;

/// <summary>
/// Configures business logic integration, such as domain event handlers,
/// or application service registrations that require authenticated context.
/// </summary>
public interface IBusinessLogicInitializationStep : IApplicationInitializationStep, IAfter<IAuthenticationInitializationStep>;

/// <summary>
/// CRTP variant of <see cref="IBusinessLogicInitializationStep"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
public interface IBusinessLogicInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : IBusinessLogicInitializationStep, IStep<TSelf>
    where TSelf : IBusinessLogicInitializationStep<TSelf>;

/// <summary>
/// Configures HTTP endpoints, route mappings, and minimal API definitions.
/// This is the final step in the application configuration pipeline,
/// ensuring all underlying infrastructure (auth, business logic, etc.) is ready.
/// </summary>
public interface IEndpointInitializationStep : IApplicationInitializationStep, IAfter<IBusinessLogicInitializationStep>;

/// <summary>
/// CRTP variant of <see cref="IEndpointInitializationStep"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
public interface IEndpointInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : IEndpointInitializationStep, IStep<TSelf>
    where TSelf : IEndpointInitializationStep<TSelf>;
