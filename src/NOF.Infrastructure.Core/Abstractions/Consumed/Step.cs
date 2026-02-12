namespace NOF.Infrastructure.Core;

/// <summary>
/// Represents a marker interface for all configuration units in the application.
/// Serves as a base contract to identify types that contribute to the application's setup process.
/// </summary>
public interface IStep;

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
/// Indicates that the implementing configurator must execute before any configurator.
/// This provides a way to declare ordering without modifying the dependent type.
/// </summary>
/// <typeparam name="TDependency">The configurator type that should run after this one.</typeparam>
public interface IBefore<TDependency> where TDependency : IStep;
