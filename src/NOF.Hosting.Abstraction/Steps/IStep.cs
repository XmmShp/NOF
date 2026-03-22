using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

/// <summary>
/// Represents a marker interface for all configuration units in the application.
/// Serves as a base contract to identify types that contribute to the application's setup process.
/// <para>
/// Each concrete step must expose its own <see cref="Type"/> so that the framework can inspect
/// implemented interfaces for dependency ordering (via <see cref="IAfter{T}"/> / <see cref="IBefore{T}"/>)
/// without relying on <see cref="object.GetType"/> which is not annotated for trimming.
/// The recommended way is to implement <see cref="IStep{TSelf}"/> (CRTP) which provides
/// a default implementation that returns <c>typeof(TSelf)</c> with the correct
/// <see cref="DynamicallyAccessedMemberTypes.Interfaces"/> annotation, making the step
/// fully compatible with Native AOT and IL trimming.
/// </para>
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStep
{
    /// <summary>
    /// Gets the concrete <see cref="System.Type"/> of this step, annotated so the trimmer
    /// preserves its interface metadata. Used by the dependency graph to discover
    /// <see cref="IAfter{T}"/> / <see cref="IBefore{T}"/> relationships at runtime.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
    Type Type { get; }
}

/// <summary>
/// CRTP (Curiously Recurring Template Pattern) helper that automatically implements
/// <see cref="IStep.Type"/> as <c>typeof(TSelf)</c>. Concrete steps should inherit from
/// the matching typed variant (e.g. <see cref="IServiceRegistrationStep{TSelf}"/>,
/// <see cref="IApplicationInitializationStep{TSelf}"/>) which in turn extends this interface.
/// </summary>
/// <typeparam name="TSelf">The concrete step type itself.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : IStep
    where TSelf : IStep<TSelf>
{
    /// <inheritdoc/>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
    Type IStep.Type => typeof(TSelf);
}

/// <summary>
/// Indicates that a configuration type has an explicit dependency on another configuration type <typeparamref name="TDependency"/>.
/// This contract enables the framework to order configuration execution based on declared dependencies,
/// ensuring that <typeparamref name="TDependency"/> is executed before the implementing type.
/// </summary>
/// <typeparam name="TDependency">
/// The configuration type this component depends on. Must implement <see cref="IStep"/>.
/// </typeparam>
public interface IAfter<TDependency> where TDependency : IStep;

/// <summary>
/// Indicates that the implementing configurator must execute before any configurator
/// of type <typeparamref name="TDependency"/>.
/// This provides a way to declare ordering without modifying the dependent type.
/// </summary>
/// <typeparam name="TDependency">The configurator type that should run after this one.</typeparam>
public interface IBefore<TDependency> where TDependency : IStep;
