namespace NOF.Contract;

/// <summary>
/// Base marker interface for request messages.
/// </summary>
public interface IRequestBase : IMessage;

/// <summary>
/// Marker interface for request messages without a response.
/// </summary>
public interface IRequest : IRequestBase;

/// <summary>
/// Marker interface for request messages that return a response.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequest<TResponse> : IRequestBase;
