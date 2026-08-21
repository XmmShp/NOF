using NOF.Contract;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthDeviceGrantService
{
    Task<Result<OAuthDeviceAuthorizationResponse>> CreateAsync(
        CreateOAuthDeviceGrantRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OAuthDeviceAuthorizationDescriptor>> GetPendingAsync(
        string userCode,
        CancellationToken cancellationToken = default);

    Task<Result> ApproveAsync(
        string userCode,
        string subject,
        CancellationToken cancellationToken = default);

    Task<Result> DenyAsync(
        string userCode,
        CancellationToken cancellationToken = default);

    Task<Result<OAuthTokenEndpointResponse>> RedeemAsync(
        string deviceCode,
        string clientId,
        CancellationToken cancellationToken = default);
}
