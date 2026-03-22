namespace NOF.UI;

public interface IHttpRequestMessageAuthorizer
{
    ValueTask AuthorizeAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}

