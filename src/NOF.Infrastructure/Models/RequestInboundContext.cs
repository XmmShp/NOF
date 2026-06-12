using NOF.Contract;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RequestInboundContext : Context
{
    public IResult? Response { get; private set; }

    public IDictionary<string, string?> ResponseHeaders { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
    public required Type ServiceType { get; init; }

    public required MethodInfo ServiceMethodInfo { get; init; }

    public required Type HandlerType { get; init; }

    public required MethodInfo HandlerMethodInfo { get; init; }

    public required Type RequestType { get; init; }

    public required Type ResponseType { get; init; }

    public void SetResponse(IResult response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Response = response;
    }

    public void SetResponse(IResult failure, bool ignoreResultResponseType = false)
    {
        ArgumentNullException.ThrowIfNull(failure);

        if (!ignoreResultResponseType && CreatesBusinessResult(ResponseType))
        {
            Response = ResultProjection.CreateFailure(ResponseType, failure);
            return;
        }

        Response = ResultProjection.CreateFailure(typeof(Result), failure);
    }

    [SetsRequiredMembers]
    private RequestInboundContext(IReadOnlyDictionary<object, object?> items, RequestInboundContext source)
        : base(items)
    {
        Response = source.Response;
        ResponseHeaders = new Dictionary<string, string?>(source.ResponseHeaders, StringComparer.OrdinalIgnoreCase);
        ServiceType = source.ServiceType;
        ServiceMethodInfo = source.ServiceMethodInfo;
        HandlerType = source.HandlerType;
        HandlerMethodInfo = source.HandlerMethodInfo;
        RequestType = source.RequestType;
        ResponseType = source.ResponseType;
    }

    public RequestInboundContext()
    {
    }

    protected override Context Clone(IReadOnlyDictionary<object, object?> items)
        => new RequestInboundContext(items, this);

    private static bool CreatesBusinessResult(Type responseType)
        => typeof(IResult).IsAssignableFrom(responseType);
}
