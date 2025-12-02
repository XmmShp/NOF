using MassTransit;

namespace NOF;

[ExcludeFromTopology]
[ExcludeFromImplementedTypes]
public interface ICommandBase;

[ExcludeFromTopology]
[ExcludeFromImplementedTypes]
public interface ICommand : ICommandBase;

[ExcludeFromTopology]
[ExcludeFromImplementedTypes]
public interface ICommand<TResponse> : ICommandBase;

[ExcludeFromTopology]
[ExcludeFromImplementedTypes]
public interface IAsyncCommand : ICommandBase;