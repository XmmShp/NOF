using NOF.Contract;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthClientRegistrationRepository
{
    Task<Result<OAuthClientRegistrationSecretDescriptor>> RegisterAsync(
        CreateOAuthClientRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OAuthClientRegistrationSecretDescriptor>> GetAsync(
        string clientId,
        string registrationAccessToken,
        CancellationToken cancellationToken = default);

    Task<Result<OAuthClientRegistrationSecretDescriptor>> UpdateAsync(
        string clientId,
        string registrationAccessToken,
        string? currentClientSecret,
        UpdateOAuthClientRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        string clientId,
        string registrationAccessToken,
        CancellationToken cancellationToken = default);
}
