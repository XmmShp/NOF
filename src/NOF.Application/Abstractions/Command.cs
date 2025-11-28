using MassTransit;

namespace NOF;

[ExcludeFromTopology]
public interface ICommandBase;

[ExcludeFromTopology]
public interface ICommand : ICommandBase;

[ExcludeFromTopology]
public interface ICommand<TResponse> : ICommandBase;

[ExcludeFromTopology]
public interface IAsyncCommand : ICommandBase;