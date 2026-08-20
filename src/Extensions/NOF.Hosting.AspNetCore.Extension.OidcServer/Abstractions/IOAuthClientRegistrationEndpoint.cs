using Microsoft.AspNetCore.Http;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthClientRegistrationEndpoint
{
    Task<IResult> RegisterAsync(
        HttpRequest httpRequest,
        OAuthClientRegistrationRequest request,
        CancellationToken cancellationToken);

    Task<IResult> GetAsync(
        HttpRequest httpRequest,
        string clientId,
        CancellationToken cancellationToken);

    Task<IResult> UpdateAsync(
        HttpRequest httpRequest,
        string clientId,
        OAuthClientRegistrationUpdateRequest request,
        CancellationToken cancellationToken);

    Task<IResult> DeleteAsync(
        HttpRequest httpRequest,
        string clientId,
        CancellationToken cancellationToken);
}
