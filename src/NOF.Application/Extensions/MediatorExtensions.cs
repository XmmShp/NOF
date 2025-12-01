using MassTransit;
using MassTransit.Mediator;

namespace NOF;

public static class MediatorExtensions
{
    extension(IMediator mediator)
    {
        /// <summary>
        /// Sends a request, with the specified response type, and awaits the response.
        /// </summary>
        /// <param name="request">The request message</param>
        /// <param name="cancellationToken"></param>
        /// <param name="timeout"></param>
        /// <typeparam name="TResponse">The response type</typeparam>
        /// <returns>The response object</returns>
        public async Task<Result<TResponse>> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default, RequestTimeout timeout = default)
        {
            return await mediator.SendRequest(new RequestWrapper<IRequest<TResponse>, TResponse>(request), cancellationToken, timeout);
        }

        /// <summary>
        /// Sends a request, with the specified response type, and awaits the response.
        /// </summary>
        /// <param name="request">The request message</param>
        /// <param name="cancellationToken"></param>
        /// <param name="timeout"></param>
        /// <returns>The response object</returns>
        public async Task<Result> SendRequest(IRequest request, CancellationToken cancellationToken = default, RequestTimeout timeout = default)
        {
            return await mediator.SendRequest(new RequestWrapper<IRequest>(request), cancellationToken, timeout);
        }
    }
}
