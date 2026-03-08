namespace NOF.Hosting.BlazorWebAssembly;

public class HttpAuthenticationHandler : DelegatingHandler
{
    private readonly IHttpRequestMessageAuthorizer _requestAuthorizer;

    public HttpAuthenticationHandler(IHttpRequestMessageAuthorizer requestAuthorizer)
    {
        ArgumentNullException.ThrowIfNull(requestAuthorizer);
        _requestAuthorizer = requestAuthorizer;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _requestAuthorizer.AuthorizeAsync(request, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
