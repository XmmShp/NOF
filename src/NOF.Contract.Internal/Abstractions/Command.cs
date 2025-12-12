namespace NOF;

public interface ICommandBase;

public interface ICommand : ICommandBase;

public interface ICommand<TResponse> : ICommandBase;

public interface IAsyncCommand : ICommandBase;