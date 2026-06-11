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

    public async ValueTask InvokeAsync(RequestInboundContext context, object request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        IResult? firstFailure = null;

        foreach (var authorizationPolicy in _authorizationPolicies)
        {
            var authorizationResult = await authorizationPolicy.AuthorizeAsync(context, cancellationToken);
            if (authorizationResult is null)
            {
                await next(context, request, cancellationToken);
                return;
            }

            firstFailure ??= authorizationResult;
        }

        if (firstFailure is not null)
        {
            context.Response = RequestInboundResponseFactory.CreateFailure(context, firstFailure, 403);
            return;
        }

        await next(context, request, cancellationToken);
    }
}
