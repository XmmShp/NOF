namespace NOF.Hosting.BlazorWebAssembly;

public interface IHttpRequestMessageAuthorizer
{
    ValueTask AuthorizeAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
