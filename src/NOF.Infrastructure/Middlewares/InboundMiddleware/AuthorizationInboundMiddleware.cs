using NOF.Contract;
using NOF.Hosting;
using System.Globalization;

namespace NOF.Infrastructure;

public sealed class AuthorizationInboundMiddleware :
    IRequestInboundMiddleware
{
    public TopologyComparison Compare(IRequestInboundMiddleware other)
        => other is TenantInboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

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
            context.SetResponse(EnsureStatusCode(firstFailure, 403), ignoreResultResponseType: false);
            return;
        }

        await next(context, request, cancellationToken);
    }

    private static IResult EnsureStatusCode(IResult failure, int fallbackStatusCode)
        => int.TryParse(failure.ErrorCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? failure
            : Result.Fail(fallbackStatusCode.ToString(CultureInfo.InvariantCulture), failure.Message, failure.Extra);
}
