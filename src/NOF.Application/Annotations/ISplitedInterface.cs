using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Marks a class as the container for generated one-method nested interfaces
/// derived from an <see cref="IRpcService"/> interface.
/// </summary>
/// <typeparam name="TService">The RPC service interface to split</typeparam>
public interface ISplitedInterface<TService>
    where TService : IRpcService;
