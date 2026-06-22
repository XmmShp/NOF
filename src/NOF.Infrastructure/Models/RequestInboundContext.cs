using NOF.Contract;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RequestInboundContext : Context
{
    private readonly RequestInboundState _state;

    public IResult? Response => _state.Response;

    public IDictionary<string, string?> ResponseHeaders => _state.ResponseHeaders;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
    public required Type ServiceType { get; init; }

    public required MethodInfo ServiceMethodInfo { get; init; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
    public required Type HandlerType { get; init; }

    public required MethodInfo HandlerMethodInfo { get; init; }

    public required Type RequestType { get; init; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
    public required Type ResponseType { get; init; }

    public void SetResponse(IResult response)
    {
        ArgumentNullException.ThrowIfNull(response);
        _state.Response = response;
    }

    public void SetResponse(IResult failure, bool ignoreResultResponseType = false)
    {
        ArgumentNullException.ThrowIfNull(failure);

        if (!ignoreResultResponseType && CreatesBusinessResult(ResponseType))
        {
            _state.Response = ResultProjection.CreateFailure(ResponseType, failure);
            return;
        }

        _state.Response = ResultProjection.CreateFailure(typeof(Result), failure);
    }

    [SetsRequiredMembers]
    private RequestInboundContext(IReadOnlyDictionary<object, object?> items, RequestInboundContext source)
        : base(items)
    {
        _state = source._state;
        ServiceType = source.ServiceType;
        ServiceMethodInfo = source.ServiceMethodInfo;
        HandlerType = source.HandlerType;
        HandlerMethodInfo = source.HandlerMethodInfo;
        RequestType = source.RequestType;
        ResponseType = source.ResponseType;
    }

    public RequestInboundContext()
    {
        _state = new RequestInboundState();
    }

    protected override Context Clone(IReadOnlyDictionary<object, object?> items)
        => new RequestInboundContext(items, this);

    private static bool CreatesBusinessResult(Type responseType)
        => typeof(IResult).IsAssignableFrom(responseType);

    private sealed class RequestInboundState
    {
        public IResult? Response { get; set; }

        public IDictionary<string, string?> ResponseHeaders { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }
}
