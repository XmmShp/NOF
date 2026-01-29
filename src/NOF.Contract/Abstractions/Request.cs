using NOF.Contract.Annotations;

namespace NOF;

public interface IRequestBase : IMessage;

public interface IRequest : IRequestBase;

public interface IRequest<TResponse> : IRequestBase;