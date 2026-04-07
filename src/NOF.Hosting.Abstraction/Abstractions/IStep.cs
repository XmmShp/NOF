namespace NOF.Hosting;

/// <summary>
/// Indicates that a configuration type has an explicit dependency on another configuration type <typeparamref name="TDependency"/>.
/// This contract enables the framework to order configuration execution based on declared dependencies,
/// ensuring that <typeparamref name="TDependency"/> is executed before the implementing type.
/// </summary>
public interface IAfter<TDependency>;

/// <summary>
/// Indicates that the implementing configurator must execute before any configurator
/// of type <typeparamref name="TDependency"/>.
/// </summary>
public interface IBefore<TDependency>;
