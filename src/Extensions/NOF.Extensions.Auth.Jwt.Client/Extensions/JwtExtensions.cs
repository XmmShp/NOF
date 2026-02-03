namespace NOF;

/// <summary>
/// Extension methods for JWT client operations.
/// </summary>
public static partial class __NOF_Extensions_Auth_Jwt_Client_Extensions__
{
    /// <summary>
    /// Gets the JSON Web Key Set (JWKS) for the specified audience.
    /// </summary>
    /// <param name="builder">The NOF application builder.</param>
    /// <param name="audience">The audience/client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The JWKS response containing issuer and keys.</returns>
    public static async Task<GetJwksResponse?> GetJwksAsync(this INOFAppBuilder builder, string audience, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder.RequestSender);
        var requestSender = builder.RequestSender;
        var request = new GetJwksRequest(audience);
        var response = await requestSender.SendAsync(request, cancellationToken: cancellationToken);
        return response.Value;
    }
}
