using MassTransit;
using MassTransit.Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace NOF;

internal static class HandlerFactory
{
    public static async Task<IResult> HandleGetAsync<TRequest>(
        [FromQuery] TRequest request,
        [FromServices] IScopedMediator mediator)
        where TRequest : class, IRequest
    {
        var response = await mediator.SendRequest(request);
        return Results.Ok(response);
    }

    public static async Task<IResult> HandleGetWithResultAsync<TRequest, TResponse>(
        [FromQuery] TRequest request,
        [FromServices] IScopedMediator mediator)
        where TRequest : class, IRequest<TResponse>
    {
        var response = await mediator.SendRequest(request);
        return Results.Ok(response);
    }

    public static async Task<IResult> HandleCommandAsync<TRequest>(
        [FromBody] TRequest request,
        [FromServices] IScopedMediator mediator)
        where TRequest : class, IRequest
    {
        var response = await mediator.SendRequest(request);
        return Results.Ok(response);
    }

    public static async Task<IResult> HandleCommandWithResultAsync<TRequest, TResponse>(
        [FromBody] TRequest request,
        [FromServices] IScopedMediator mediator)
        where TRequest : class, IRequest<TResponse>
    {
        var response = await mediator.SendRequest(request);
        return Results.Ok(response);
    }

    /// <summary>
    /// Creates a delegate compatible with Minimal API endpoint registration.
    /// </summary>
    /// <param name="requestType">Must implement IRequest or IRequest&lt;T&gt;</param>
    /// <param name="isQuery">true for GET (uses [FromQuery]), false for POST/PUT/etc (uses [FromBody])</param>
    /// <returns>A delegate of type Func&lt;TRequest, IScopedMediator, CancellationToken, Task&lt;IResult&gt;&gt;</returns>
    public static Delegate Create(Type requestType, bool isQuery)
    {
        var responseType = GetResponseType(requestType);

        MethodInfo methodInfo;

        if (isQuery)
        {
            if (responseType is null)
            {
                methodInfo = typeof(HandlerFactory)
                    .GetMethod(nameof(HandleGetAsync), BindingFlags.Public | BindingFlags.Static)!
                    .MakeGenericMethod(requestType);
            }
            else
            {
                methodInfo = typeof(HandlerFactory)
                    .GetMethod(nameof(HandleGetWithResultAsync), BindingFlags.Public | BindingFlags.Static)!
                    .MakeGenericMethod(requestType, responseType);
            }
        }
        else
        {
            if (responseType is null)
            {
                methodInfo = typeof(HandlerFactory)
                    .GetMethod(nameof(HandleCommandAsync), BindingFlags.Public | BindingFlags.Static)!
                    .MakeGenericMethod(requestType);
            }
            else
            {
                methodInfo = typeof(HandlerFactory)
                    .GetMethod(nameof(HandleCommandWithResultAsync), BindingFlags.Public | BindingFlags.Static)!
                    .MakeGenericMethod(requestType, responseType);
            }
        }

        // Build delegate signature: Func<TRequest, IScopedMediator, CancellationToken, Task<IResult>>
        var delegateType = typeof(Func<,,>)
            .MakeGenericType(requestType, typeof(IScopedMediator), typeof(Task<IResult>));

        return methodInfo.CreateDelegate(delegateType);
    }

    private static Type? GetResponseType(Type requestType)
    {
        return requestType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType
                                 && i.GetGenericTypeDefinition() == typeof(IRequest<>))?
            .GetGenericArguments()
            .FirstOrDefault();
    }
}