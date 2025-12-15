using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NOF;

internal enum FromType
{
    FromQuery,
    FromBody
}

internal interface IRequestDelegateFactory
{
    Delegate Create(FromType from);
}

internal class RequestDelegateFactory<TRequest, TResponse> : IRequestDelegateFactory
    where TRequest : IRequest<TResponse>
{
    public Delegate Create(FromType from)
    {
        if (from == FromType.FromBody)
        {
            return async ([FromBody] TRequest request, [FromServices] IRequestSender sender) =>
            {
                var response = await sender.SendAsync(request);
                return TypedResults.Ok(response);
            };
        }

        // FromType.FromQuery
        return async ([AsParameters] TRequest request, [FromServices] IRequestSender sender) =>
        {
            var response = await sender.SendAsync(request);
            return TypedResults.Ok(response);
        };
    }
}

internal class RequestDelegateFactory<TRequest> : IRequestDelegateFactory
    where TRequest : IRequest
{
    public Delegate Create(FromType from)
    {
        if (from == FromType.FromBody)
        {
            return async ([FromBody] TRequest request, [FromServices] IRequestSender sender) =>
            {
                var response = await sender.SendAsync(request);
                return TypedResults.Ok(response);
            };
        }

        // FromType.FromQuery
        return async ([AsParameters] TRequest request, [FromServices] IRequestSender sender) =>
        {
            var response = await sender.SendAsync(request);
            return TypedResults.Ok(response);
        };
    }
}
