using MassTransit;
using MassTransit.Mediator;
using NOF.Contract;
using System.Runtime.ExceptionServices;

namespace NOF.Infrastructure.MassTransit;

public static partial class NOFInfrastructureMassTransitExtensions
{
    extension(IMediator mediator)
    {
        public async Task<Result> SendRequest(IRequest request, IDictionary<string, string?>? headers = null, RequestTimeout timeout = default, CancellationToken cancellationToken = default)
        {
            try
            {
                using var handle = mediator.CreateRequest(request, cancellationToken, timeout);
                SetHeaders(handle, headers);
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

        public async Task<Result<TResponse>> SendRequest<TResponse>(IRequest<TResponse> request, IDictionary<string, string?>? headers = null, RequestTimeout timeout = default, CancellationToken cancellationToken = default)
        {
            try
            {
                using var handle = mediator.CreateRequest(request, cancellationToken, timeout);
                SetHeaders(handle, headers);
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

    private static void SetHeaders<T>(RequestHandle<T> handle, IDictionary<string, string?>? headers)
        where T : class
    {
        if (headers is null || headers.Count == 0)
            return;

        handle.UseExecute(context =>
        {
            foreach (var header in headers)
            {
                context.Headers.Set(header.Key, header.Value);
            }
        });
    }
}
