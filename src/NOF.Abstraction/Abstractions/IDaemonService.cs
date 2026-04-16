namespace NOF.Abstraction;

/// <summary>
/// Marker interface for services that must be instantiated immediately after a new scope is created.
/// Register implementations as scoped services.
/// </summary>
public interface IDaemonService;
