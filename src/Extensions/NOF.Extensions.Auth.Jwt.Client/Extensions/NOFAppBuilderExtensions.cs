namespace NOF;

/// <summary>
/// Extension methods for JWT client operations.
/// </summary>
public static partial class __NOF_Extensions_Auth_Jwt_Client_Extensions__
{
    /// <param name="builder">The NOF application builder.</param>
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Gets the JSON Web Key Set (JWKS) for the specified audience.
        /// </summary>
        /// <param name="audience">The audience/client identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The JWKS response containing issuer and keys.</returns>
        public async Task<GetJwksResponse?> GetJwksAsync(string audience, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(builder.RequestSender);
            var requestSender = builder.RequestSender;
            var request = new GetJwksRequest(audience);
            var response = await requestSender.SendAsync(request, cancellationToken: cancellationToken);
            return response.Value;
        }
    }
}
