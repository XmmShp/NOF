using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class AuthorizationInboundMiddleware :
    IRequestInboundMiddleware,
    IAfter<TenantInboundMiddleware>
{
    private readonly IReadOnlyList<IRequestAuthorizationPolicy> _authorizationPolicies;

    public AuthorizationInboundMiddleware(IEnumerable<IRequestAuthorizationPolicy> authorizationPolicies)
    {
        _authorizationPolicies = [.. authorizationPolicies];
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        IResult? firstFailure = null;

        foreach (var authorizationPolicy in _authorizationPolicies)
        {
            var authorizationResult = await authorizationPolicy.AuthorizeAsync(context, cancellationToken);
            if (authorizationResult is null)
            {
                await next(cancellationToken);
                return;
            }

            firstFailure ??= authorizationResult;
        }

        if (firstFailure is not null)
        {
            context.Response = RpcResults.Fail(ParseStatusCode(firstFailure, 403), firstFailure.Message);
            return;
        }

        await next(cancellationToken);
    }

    private static int ParseStatusCode(IResult result, int fallbackStatusCode)
        => int.TryParse(result.ErrorCode, out var statusCode) ? statusCode : fallbackStatusCode;
}
