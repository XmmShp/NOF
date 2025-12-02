using MassTransit;
using MassTransit.Mediator;
using System.Runtime.ExceptionServices;

namespace NOF;

public static class MediatorExtensions
{
    extension(IMediator mediator)
    {
        public async Task<Result> SendRequest(IRequest request, CancellationToken cancellationToken = default, RequestTimeout timeout = default)
        {
            try
            {
                using var handle = mediator.CreateRequest(request, cancellationToken, timeout);
                var response = await handle.GetResponse<Result>().ConfigureAwait(false);
                return response.Message;
            }
            catch (RequestException exception)
            {
                if (exception.InnerException is not null)
                {
                    var dispatchInfo = ExceptionDispatchInfo.Capture(exception.InnerException);
                    dispatchInfo.Throw();
                }

                throw;
            }
        }

        public async Task<Result<TResponse>> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default, RequestTimeout timeout = default)
        {
            try
            {
                using var handle = mediator.CreateRequest(request, cancellationToken, timeout);
                var response = await handle.GetResponse<Result<TResponse>>().ConfigureAwait(false);
                return response.Message;
            }
            catch (RequestException exception)
            {
                if (exception.InnerException is not null)
                {
                    var dispatchInfo = ExceptionDispatchInfo.Capture(exception.InnerException);
                    dispatchInfo.Throw();
                }

                throw;
            }
        }
    }
}
