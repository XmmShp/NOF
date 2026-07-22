using NOF.Contract;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthClientRepository
{
    ValueTask<OAuthClientCredentialsValidationResult> ValidateClientCredentialsAsync(
        OAuthClientCredentialsValidationRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OAuthClientDescriptor>> ListAsync(CancellationToken cancellationToken = default);

    Task<Result<OAuthClientDescriptor>> GetAsync(string clientId, CancellationToken cancellationToken = default);

    Task<Result<OAuthClientSecretDescriptor>> CreateAsync(
        CreateOAuthClientRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OAuthClientDescriptor>> UpdateAsync(
        string clientId,
        UpdateOAuthClientRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OAuthClientSecretDescriptor>> RotateSecretAsync(
        string clientId,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(string clientId, CancellationToken cancellationToken = default);
}
