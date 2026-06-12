using Microsoft.IdentityModel.Tokens;
using NOF.Contract;
using System.Security.Cryptography;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthAuthorizationHandler
{
    ValueTask<OAuthAuthorizationResult> AuthorizeAsync(
        OAuthAuthorizationRequest request,
        CancellationToken cancellationToken);
}

public interface IOAuthAuthorizationCodeService
{
    ValueTask<string> CreateAsync(
        OAuthAuthorizationCodeDescriptor descriptor,
        CancellationToken cancellationToken);
}

public interface IOAuthSubjectService
{
    ValueTask<OAuthSubjectProfile?> GetProfileAsync(
        string subject,
        IReadOnlySet<string> scopes,
        CancellationToken cancellationToken);

    ValueTask<bool> CanRefreshAsync(
        string subject,
        string refreshTokenId,
        IReadOnlySet<string> scopes,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(true);
}

public interface ITokenService
{
    Task<Result<IssueTokenResponse>> IssueTokenAsync(IssueTokenRequest request, CancellationToken cancellationToken);

    Task<Result<ValidateRefreshTokenResponse>> ValidateRefreshTokenAsync(ValidateRefreshTokenRequest request, CancellationToken cancellationToken);

    Task<Result> RevokeRefreshTokenAsync(RevokeRefreshTokenRequest request, CancellationToken cancellationToken);
}

public interface IJwksService
{
    Task<JwksDocument> GetJwksAsync(CancellationToken cancellationToken = default);
}

public interface ISigningKeyService
{
    Task<ManagedSigningKey> GetCurrentSigningKeyAsync(CancellationToken cancellationToken = default);

    Task<ManagedSigningKey[]> GetAllKeysAsync(CancellationToken cancellationToken = default);

    Task RotateKeyAsync(CancellationToken cancellationToken = default);
}

public static class JwksSecurityKeyConverter
{
    public static SecurityKey[] ToSecurityKeys(JwkKeyDocument[] jwkKeys)
    {
        var keys = new List<SecurityKey>();

        foreach (var jwk in jwkKeys)
        {
            if (jwk.Kty != "RSA" || string.IsNullOrWhiteSpace(jwk.N) || string.IsNullOrWhiteSpace(jwk.E))
            {
                continue;
            }

            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Base64UrlDecode(jwk.N),
                Exponent = Base64UrlDecode(jwk.E)
            });

            keys.Add(new RsaSecurityKey(rsa) { KeyId = jwk.Kid });
        }

        return keys.ToArray();
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2:
                output += "==";
                break;
            case 3:
                output += "=";
                break;
        }

        return Convert.FromBase64String(output);
    }
}
