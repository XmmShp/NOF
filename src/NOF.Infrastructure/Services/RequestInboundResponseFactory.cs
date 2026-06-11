using NOF.Abstraction;
using NOF.Contract;
using System.Globalization;

namespace NOF.Infrastructure;

internal static class RequestInboundResponseFactory
{
    public static IRpcResult CreateFailure(RequestInboundContext context, IResult failure, int fallbackStatusCode = 500)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(failure);

        if (CreatesBusinessResult(context.ResponseType))
        {
            return CreateBusinessFailure(context.ResponseType, failure);
        }

        return RpcResults.Fail(HttpTransportMetadata.Create(ParseStatusCode(failure.ErrorCode, fallbackStatusCode)));
    }

    private static bool CreatesBusinessResult(Type responseType)
        => typeof(IResult).IsAssignableFrom(responseType);

    private static IRpcResult CreateBusinessFailure(Type responseType, IResult failure)
        => RpcResults.BusinessFailure(responseType, failure);

    private static int ParseStatusCode(string? errorCode, int fallbackStatusCode)
        => int.TryParse(errorCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCode)
            ? statusCode
            : fallbackStatusCode;
}
