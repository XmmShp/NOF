namespace NOF.Hosting;

/// <summary>
/// Marker interface for services that must be instantiated immediately after the root service provider is built.
/// Register implementations as singleton services.
/// </summary>
public interface IDaemonService;

/// <summary>
/// Marker interface for services that must be instantiated immediately after a new scope is created.
/// Register implementations as scoped services.
/// </summary>
public interface IScopedDaemonService;

