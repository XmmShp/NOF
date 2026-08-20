using Microsoft.AspNetCore.Http;
using NOF.Contract;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

/// <summary>
/// Authorizes calls to the dynamic client registration endpoint using an initial access token.
/// Replace this service to implement a different admission policy, including anonymous registration.
/// </summary>
public interface IOAuthInitialAccessTokenHandler
{
    Task<Result> HandleAsync(HttpRequest request, CancellationToken cancellationToken);
}
